# Launchpad Mini Mk.3

The Launchpad Mini Mk.3 can be used to control the looper in the mixer channels.

## Launchpad Initialization

The Launchpad exposes two PipeWire MIDI port pairs:

- `Launchpad Mini MK3: LPMiniMK3 MI (capture)`
- `Launchpad Mini MK3: LPMiniMK3 MI (playback)`
- `Launchpad Mini MK3: LPMiniMK3 DA (capture)`
- `Launchpad Mini MK3: LPMiniMK3 DA (playback)`

The matching aliases are:

- `Launchpad Mini MK3:Launchpad Mini MK3 LPMiniMK3 MI`
- `Launchpad Mini MK3:Launchpad Mini MK3 LPMiniMK3 DA`

When the Launchpad appears, Sonic Eddy should connect both capture ports so it
can receive events from both the MI and DA interfaces. Initialization itself
must run only once per device connection, on the first available MI playback
port. After initialization enables the DAW/session interface, normal controller
communication uses the DA ports.

The first-connect MI initialization uses the message sequence from
`/home/frank/Development/pmx/src/applications/lp_mini_init.cpp`. That sequence
is known-good and should be copied into Sonic Eddy's controller architecture
rather than redesigned.

1. Send the device ID request until the expected Launchpad Mini response is
   received or the init attempt times out.
2. Enable session/DAW mode.
3. Send the initial session state message.

Layer selection, session LED updates, fader bank setup, and selected controller
surface setup are not part of first-connect initialization. Those messages are
sent later on the DA playback port when Sonic Eddy configures the currently
selected Launchpad surface.

## Button Mapping

The mapping has two modes: channel, and group + master.

### Mode Selection

Modes can be selected with two Session-mode CC buttons in the rightmost column,
starting with the 3rd button from the top (`69`, `59`). The selected mode's
button is colored in bright blue, the not selected one in dark blue.

Whenever Sonic Eddy's selected mode, selected layer, or the Launchpad layout
changes, the controller refreshes the full Launchpad state.

## Mode Mapping

The modes only differ in which channels they control:

- _Channel_: Each column controls a channel of the selected layer
- _Group + Master_: The first four columns control a group channel of the
  selected layer, the fifth channel is mapped to the master of the layer

### Session Mode

Each column controls the active looper of one channel in the selected layer and
mode. Each row maps to one loop position in that channel's active looper. The
LED coding for each button is:

- `static dark green` => No loop loaded
- `static bright green` => Loop loaded
- `blinking bright green` => Playing

Pressing the button has the following behavior:

- if the looper has a loop for the button => send play based on the current
  looper settings in the UI
- else, send cut/play based on the current looper settings in the UI

Inactive or unavailable columns have all LEDs off. Button presses in inactive or
unavailable columns are ignored.

Loop positions are mapped by row. Columns select the controlled channel:

| Loop position | Column 1 | Column 2 | Column 3 | Column 4 | Column 5 | Column 6 | Column 7 | Column 8 |
| --------------- | -------- | -------- | -------- | -------- | -------- | -------- | -------- | -------- |
| 0               | `81`     | `82`     | `83`     | `84`     | `85`     | `86`     | `87`     | `88`     |
| 1               | `71`     | `72`     | `73`     | `74`     | `75`     | `76`     | `77`     | `78`     |
| 2               | `61`     | `62`     | `63`     | `64`     | `65`     | `66`     | `67`     | `68`     |
| 3               | `51`     | `52`     | `53`     | `54`     | `55`     | `56`     | `57`     | `58`     |
| 4               | `41`     | `42`     | `43`     | `44`     | `45`     | `46`     | `47`     | `48`     |
| 5               | `31`     | `32`     | `33`     | `34`     | `35`     | `36`     | `37`     | `38`     |
| 6               | `21`     | `22`     | `23`     | `24`     | `25`     | `26`     | `27`     | `28`     |
| 7               | `11`     | `12`     | `13`     | `14`     | `15`     | `16`     | `17`     | `18`     |

### Fader Mode

Fader mode is set up as a DAW fader bank on the DA playback port. Since each
column controls one channel, the fader bank uses vertical orientation so fader
indices map left-to-right across the columns.

The fader bank setup message is:

```text
F0 00 20 29 02 0D 01 00 00
  00 01 00 15
  01 01 01 15
  02 01 02 15
  03 01 03 15
  04 01 04 15
  05 01 05 15
  06 01 06 15
  07 01 07 15
F7
```

The message configures:

- orientation `00`: vertical faders
- fader indices `00` - `07`: left to right
- fader type `01`: bipolar
- CC numbers `00` - `07`: one CC per column on DAW fader channel 5
- color `15`: green

Each fader is mapped to the cross fader of the active looper for the channel
its column controls. CC values are mapped to `[-1, 1]`, where lower values move
towards the dry signal, the center value is neutral, and higher values move
towards the wet signal.

Switching between Launchpad Session layout and DAW Fader layout triggers a full
state refresh after the layout change.

## Layer Selection

The layer can be selected with the top two Session-mode CC buttons (`89`, `79`)
in the rightmost column. The selected layer's button will be colored in bright
yellow, the not selected one in dark yellow.

## Notes

- The Launchpad does not directly control looper settings such as cut/play
  options. The UI remains the owner of those settings.
- When looper settings change in the UI, the C# view models send the updated
  settings to the native controller implementation.
- When view models are created, C# sends the relevant channel, looper, and
  setting information to the controller, following the same setup pattern used
  by the other controllers.
- The native controller tracks the latest settings and uses them when Launchpad
  button presses trigger play or cut/play commands.
