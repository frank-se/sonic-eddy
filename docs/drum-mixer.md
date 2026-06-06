# Drum Mixer

The drum mixer provides mixing of 16 mono channels to one stereo. It has 16 mono
inputs and 1 stereo output. The mixer is implemented as a PipeWire filter chain
using built-in `copy` and `mixer` nodes for routing and summing. Each mono input
is connected to a built-in `copy` node. The copy output is connected to both the
left and right inputs of FIL4, producing identical left and right signals before
the stereo channel effects. Links for the mixer are managed by Sonic Eddy, not
WirePlumber.

## Configuration And Lifecycle

Application preferences contain a `DrumMixerEnabled` flag. The drum mixer and
its PipeWire filter-chain nodes are created during application initialization
only when this flag is enabled. When it is disabled, no drum mixer nodes or
links are created.

The drum mixer window is opened from `Tools` -> `Drum Mixer ...`. The menu item
is always visible but is disabled while `DrumMixerEnabled` is false.

Changing `DrumMixerEnabled` requires an application restart. The preference UI
must communicate that the change takes effect after restart.

Before creating the graph, Sonic Eddy verifies that every required LV2 plugin is
available. If plugin discovery or graph creation fails, Sonic Eddy logs an
error, destroys any partially created drum mixer resources, and disables the
drum mixer for the remainder of the application session. The menu item is
disabled and the normal mixer does not expose a drum mixer routing target.

## State Persistence

The drum mixer has its own persisted state, separate from application
preferences and the normal mixer state. Disabling `DrumMixerEnabled` prevents
the graph from being created but does not delete or reset the saved drum mixer
state.

The following state is saved:

- Channel names
- Each channel's selected `Audio From` node and port
- Channel volume
- Room and Plate send levels for every channel
- All exposed FIL4, Transient Designer, Compressor, and Saturator parameter
  values, including bypass state
- Room and Plate return gains
- All exposed Dragonfly Room and Plate parameter values

State is saved after user changes using the same serialized application-data
approach as other persistent Sonic Eddy configuration. Rapid parameter changes
must be debounced so that dragging a slider does not write the state file for
every intermediate value.

After the filter graph is created, saved values are explicitly applied through
the same runtime parameter-setting paths used by the UI. The implementation
must not rely on plugin defaults, PipeWire metadata, or WirePlumber remembering
previous values. Applying all restored state through the normal control paths
keeps startup and interactive changes behaviorally identical.

When no saved state exists, a complete default drum mixer state is created and
applied through the same restoration path. Loading older state with missing
fields uses defaults for only those fields. Unknown fields or parameters are
ignored so state remains forward and backward compatible.

## Default State

The complete default state is explicitly applied after graph creation:

- Channel volume: `1.0`
- Room send: `0.0`
- Plate send: `0.0`
- Room return gain: `1.0`
- Plate return gain: `1.0`
- FIL4, Transient Designer, Compressor, and Saturator: bypassed or set to their
  defined neutral settings
- Dragonfly Room dry level: `0.0`
- Dragonfly Plate dry level: `0.0`

Dragonfly's installed default dry level is not suitable for a send effect and
must always be overridden. The remaining Dragonfly parameters initially use
their plugin defaults unless a different value is present in saved state.

## Input Routing And Link Management

Each channel's `Audio From` selector lists audio output ports belonging to
playback nodes. Selection happens at port level rather than node level because
the expected sources include individual channels of multichannel pro-audio
interfaces.

The source list excludes ports belonging to every node created by Sonic Eddy.
This includes the drum mixer itself and all other internal mixer, looper,
monitoring, synchronization, and utility nodes. Unlike the normal mixer's source
discovery, the drum mixer has no exception for its own internal nodes.

Sonic Eddy owns and manages the links from the selected source ports to the drum
mixer inputs. Changing an `Audio From` selection removes the link previously
owned by that channel and creates a link from the newly selected playback-node
port. Links not created by the drum mixer are not modified.

Input selections are persisted and restored using the same approach as MIDI
sync and other managed links. Saved state identifies each source using stable
node and port names rather than transient PipeWire object IDs. On startup and
when PipeWire objects change, the drum mixer resolves the saved endpoints and
reconciles its desired links with the currently available ports.

If a saved source port is unavailable, the selection remains configured but
unresolved. No replacement port is selected automatically. The link is created
when the matching node and port become available again.

### Main Mixer Integration

The drum mixer's stereo output is exposed through its playback node. The normal
mixer's channel `Audio From` selectors must include this playback node so the
complete drum mix can be routed into a normal mixer channel.

The playback node uses a fixed node name that identifies it as the drum mixer
output and exposes two output ports with `audio.position = [ FL FR ]`. Saved
routing resolves this fixed node name together with the FL or FR port name.

Normal mixer source discovery currently excludes Sonic Eddy's own nodes. This
filter remains in place, with a specific exception for the drum mixer output
playback node when the drum mixer is enabled and available. Other internal Sonic
Eddy nodes remain excluded.

The drum mixer's stricter source filtering prevents its output from being routed
back into the drum mixer and also prevents accidental routing from other
internal Sonic Eddy nodes.

Each mixer channel has the following fixed effect chain:

- <http://gareus.org/oss/lv2/fil4#stereo>
- <http://calf.sourceforge.net/plugins/TransientDesigner>
- <http://calf.sourceforge.net/plugins/Compressor>
- <http://calf.sourceforge.net/plugins/Saturator>

The effects default to bypassed or neutral settings. The channel strip exposes a
focused subset of each plugin's controls rather than the complete plugin
interface.

After the channel effects, the stereo signal branches into three buses:

```text
mono input -> Copy -> FIL4 L/R -> Transient Designer -> Compressor -> Saturator
                                                                       ├-> Dry bus
                                                                       ├-> Room bus
                                                                       └-> Plate bus
```

`Copy:Out` is linked to both the FIL4 left input and FIL4 right input.

The Dry bus uses the channel volume as the gain for both left and right. The
Room and Plate bus gains are controlled by the corresponding send controls and
also use the same gain for the left and right channels.

This placement makes both sends post-effects and pre-fader.

## Reverb Returns

The mixer has two shared stereo reverb returns:

- Room: <urn:dragonfly:room>
- Plate: <urn:dragonfly:plate>

Both Dragonfly reverbs operate fully wet. Their outputs and the dry bus are
summed by a final pair of built-in mixer nodes:

```text
Dry L/R -------------------------+
Room L/R  -> Dragonfly Room -----+-> Final mixer L/R -> drum mixer output
Plate L/R -> Dragonfly Plate ----+
```

The final mixer gains for Room and Plate provide the independent return gains.
The returns do not consume any of the 16 drum input channels.

## Built-in Mixer Topology

PipeWire's built-in `mixer` node is mono and supports at most eight inputs. Every
stereo bus therefore uses matching left and right mixer trees. Each tree first
sums channels 1-8 and channels 9-16 separately, then sums those two intermediate
outputs:

```text
Channels 1-8  -> Mixer L/R --+
                             +-> Bus output L/R
Channels 9-16 -> Mixer L/R --+
```

This structure is used for the Dry, Room, and Plate buses. A final pair of
three-input mixers sums the Dry output, wet Room output, and wet Plate output.
All routing is represented by ordinary filter-graph edges; the design does not
use hidden or named plugin connection points.

The channel volumes, sends, and return gains map to built-in mixer `Gain N`
controls. Built-in mixer controls must therefore support runtime updates and
state observation in the same way as exposed LV2 controls; they cannot remain
initial configuration values only.

## User Interface

The drum mixer uses the same visual language as the main mixer, but keeps the
16 channel strips focused on controls needed while mixing. Each channel strip
contains:

- A channel header showing the channel name
- An `Audio From` selector directly below the channel header
- Two send controls labelled `Room` and `Plate`
- A volume control and the existing monitoring display

The send controls use the same slider design as the existing mixer controls.
The drum mixer only exposes these two fixed sends rather than the main mixer's
four generic sends. Drum channels do not expose pan controls.

Selecting a channel header selects that channel and updates the plugin parameter
area. The existing `ChannelHeader` control can be reused without its delete
button.

### Plugin Parameters

Plugin parameters are displayed in a section below the channel strips. This
section spans the available mixer width instead of using the main mixer's narrow
details pane.

For a selected drum channel, the parameter area displays one section for each
plugin, arranged next to each other in signal-flow order:

1. FIL4
2. Transient Designer
3. Compressor
4. Saturator

Each section reuses the existing `ParameterGrid` control and only exposes the
curated parameters supported by the drum mixer. The FIL4 section may be wider
than the other sections because it has more exposed controls. Plugins are shown
simultaneously rather than selected through the existing paged
`ParametersEditor`.

The Room and Plate return channels are also selectable. Selecting a return
displays the parameter grid for its Dragonfly reverb in the same bottom
parameter area.

Each return strip contains:

- A fixed `Room` or `Plate` channel header
- A return volume control
- The existing stereo monitoring display

Return strips do not contain `Audio From`, send, or pan controls. Selecting the
return header selects that return and updates the bottom parameter area.
