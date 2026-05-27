# Looper UI

The looper UI has two layers:

1. A compact channel-strip section for the most common performance actions.
2. A detailed looper view for slot management, inspection, archiving, and
   explicit scheduling.

The channel strip is very narrow, so it must not expose the full looper model.
It should present a small, predictable control surface and derive its display
state from the looper params, especially `looper.state`.

Each channel has two loopers:

- Pre-FX looper, placed before the channel's effects/filter chain.
- Post-FX looper, placed after the channel's effects/filter chain.

The compact channel-strip section controls one of these two loopers at a time.
The user can switch the pickup point with a pre/post-FX toggle.

## Channel Strip Section

The channel strip section is the always-visible looper control. It should show
only the information and controls needed during normal channel operation.

The strip controls:

- Mix control for the looper wet signal, using the same compact slider control
  currently used for pan.
- Bar-length selector using a numeric selector control.
- First-beat quantize toggle for cut/play scheduling.
- Pre/post-FX looper pickup toggle.
- Icon button for cut/play.
- Icon button for stop.
- Icon button to open the detailed looper view.
- Small icon/status indicator for empty/ready/playing/archive activity.

The strip does not expose loop slot numbers. Slot selection is automatic.

The strip should avoid visible text labels. Button contents should be icons from
the icon font already used by the application. Text should be provided through
tooltips, not as visible labels, so the control fits in the channel strip.

The compact control set:

| Purpose | Control |
| --- | --- |
| Mix | Existing pan-style slider |
| Bar length | Numeric selector |
| First-beat quantize | Toggle button |
| Pre/post-FX pickup | Toggle button |
| Cut/play | Icon button |
| Stop | Icon button |
| Details | Icon button |
| State | Icon/status indicator |

Icon font mapping:

| Action | Icon codepoint |
| --- | --- |
| Cut/play | `0xF608` |
| Stop | `0xF759` |
| First-beat quantize | `0xF3FC` |
| Details | `0xF692` |
| Pre-FX pickup | `0xF695` |
| Post-FX pickup | `0xF69F` |

The compact section should use the existing channel-strip visual density and
button language. It should not introduce wider text buttons.

Exact compact layout may be chosen during implementation. The first
implementation should make a good compact-layout guess that fits the current
channel strip and can be refined after use.

## Pre-FX and Post-FX Loopers

Each channel owns two loopers so the user can capture either the dry/pre-effect
signal or the processed/post-effect signal.

The pre-FX looper captures before the channel effects/filter chain. It is useful
when the loop should continue through the current effects and respond to later
effect changes.

The post-FX looper captures after the channel effects/filter chain. It is useful
when the loop should preserve the processed sound at the moment it was cut.

The compact strip pre/post-FX toggle selects which looper receives commands and
which looper state is displayed. It does not move an existing loop between
loopers.

The detailed looper view should make both loopers visible and clearly identify
whether each slot belongs to the pre-FX or post-FX looper.

## Mixer Wiring

When building a mixer channel, the graph should insert two loopers into the
channel path:

1. Pre-FX looper between the input playback node and the next channel stage.
2. Post-FX looper at the channel output position.

The mixer channel model should store both looper modules explicitly:

- `PreFxLooper`
- `PostFxLooper`

The existing input loopback remains the channel input node pair. The existing
output loopback role is replaced by `PostFxLooper`.

There is no required migration path for old mixer state for the initial looper
integration.

Looper creation config:

- Both loopers must receive a stable channel-specific name and tag.
- Both loopers must receive the configured `archive_folder_path`.
- Both loopers should start with `mix = 0`.
- Capture/playback targets should be set during creation whenever the target is
  known, so WirePlumber can auto-link the graph.

Default archive folder:

```text
<local app data>/SonicEddy/loop_archives/looper_<channel_number>_<pre|post>
```

For example:

```text
<local app data>/SonicEddy/loop_archives/looper_1_pre
<local app data>/SonicEddy/loop_archives/looper_1_post
```

The mixer should pass this path as `archive_folder_path` when creating each
looper. The directory may be created lazily by the archive writer.

### Pre-FX Looper Wiring

The pre-FX looper is inserted after the channel input playback node.

If the channel has a filter/effects chain:

```text
input playback -> pre-FX looper capture/playback -> filter capture
```

If the channel has no filter/effects chain:

```text
input playback -> pre-FX looper capture/playback -> output capture
```

This lets the pre-FX looper capture the signal before effects. Its playback then
continues through the rest of the normal channel path.

In object terms:

- Input playback target: pre-FX looper capture.
- Pre-FX looper playback target:
  - filter capture when a filter chain exists.
  - post-FX looper capture when no filter chain exists.

When a filter chain is added or removed, the mixer must retarget the pre-FX
looper playback node:

- Filter added: pre-FX looper playback -> filter capture.
- Filter removed: pre-FX looper playback -> post-FX looper capture.

### Post-FX Looper Wiring

The post-FX looper replaces the existing output loopback role.

Instead of:

```text
channel processing -> output loopback -> mixer/output
```

the channel should use:

```text
channel processing -> post-FX looper capture/playback -> mixer/output
```

This lets the post-FX looper capture the fully processed channel signal and play
it back from the channel output point.

In object terms:

- If a filter chain exists, filter playback targets post-FX looper capture.
- If no filter chain exists, pre-FX looper playback targets post-FX looper
  capture.
- Post-FX looper playback targets the channel's routing destination, for
  example a group/master input capture node or an external output capture node.

### Volume and Pan Controls

When the post-FX looper replaces the output loopback, channel volume and pan
must control the playback side of the post-FX looper instead of the old output
loopback playback node.

The user-facing volume and pan controls do not change. Only their target node
changes:

```text
old target: output loopback playback node
new target: post-FX looper playback node
```

This keeps channel level and pan behavior after the post-FX looper and avoids
adding another output-stage node only for control.

Routing target changes must also retarget the post-FX looper playback node.
Anywhere the old channel implementation changed the output loopback playback
target, it should now change the post-FX looper playback target.

### Compact UI Target

The compact pre/post-FX toggle selects which looper receives UI commands:

- Pre-FX selected: commands and mix updates go to the pre-FX looper capture
  node.
- Post-FX selected: commands and mix updates go to the post-FX looper capture
  node.

Both loopers should still be represented in the detailed looper view.

### Switching Pre/Post-FX Selection

Switching the compact UI target must be explicit and safe because the two
loopers sit at different points in the channel graph.

When the user switches from the currently selected looper to the other looper:

1. Read the currently selected looper state and mix.
2. If the currently selected looper is playing and `mix != 0`, reject the
   switch.
3. Otherwise set `mix = 0` on the currently selected looper.
4. Set `mix = 0` on the newly selected looper.
5. Send `stop` to both loopers so neither playback path remains active.
6. Update the internally selected looper.

The rejected case prevents the UI from silently changing the pickup point while
an audible loop is playing. The user must stop playback or set mix to `0`
before switching.

When a switch is rejected, the compact UI should make the play and pre/post
toggle button icons red. This gives immediate feedback without adding visible
text to the channel strip. Detailed text may be provided as tooltip content.

After a successful switch, the compact strip displays and controls the newly
selected looper. Existing loops in the other looper remain stored and can still
be inspected from the detailed view.

## Strip Slot Policy

The channel strip always targets the first empty loop slot when cutting a new
loop.

If at least one slot is empty:

1. Select the lowest-numbered empty slot.
2. Schedule `cut <length_in_beats> <slot>` for the selected beat.
3. Schedule `play <slot>` for the same beat.

If no slot is empty:

1. Select an archive candidate.
2. Archive that candidate.
3. Wait until `looper.state` shows the slot as empty.
4. Schedule the cut/play command into the freed slot.

The strip must never silently overwrite or drop an existing populated loop.

## Archive Candidate Selection

When all slots are populated, the strip chooses an archive candidate using a
deterministic policy:

1. Only non-playing populated slots are eligible.
2. Prefer the oldest slot by `end_beat` when available.
3. Otherwise prefer the lowest generation.
4. If still tied, choose the lowest slot number.

If no non-playing slot is available, the strip disables the cut action or shows
a blocked state.

Auto-archive is a client/UI policy. The native looper only receives normal
`archive`, `cut`, and `play` commands.

Archive requires the looper to have been created with an `archive_folder_path`.
If archiving is unavailable or fails, the UI must not cut into an occupied slot.

## Strip Command Flow

The normal cut/play flow sends both commands for the same beat. The UI converts
the selected bar length to beats before sending the command:

```json
[
  [123, "cut 16 0"],
  [123, "play 0"]
]
```

The target slot is selected by the UI immediately before scheduling.

When the strip needs to auto-archive, archive and cut/play are separate phases.
The UI must observe the state transition before scheduling the cut:

1. Schedule `archive <slot>`.
2. Wait for `looper.state` to omit that slot.
3. Schedule `cut <length_in_beats> <slot>` and `play <slot>`.

This follows the native looper behavior: archive clears the slot once the
archive job is accepted. The UI may schedule the next cut after the empty slot
is visible in `looper.state`.

## Strip Display State

The strip should derive display state from `looper.state`:

- `recording`: transport recording state.
- `active_loop`: currently playing slot, if any.
- `loops`: populated slots and their metadata.
- `active_playback`: playhead and start beat for the active loop.
- `pending_jobs`: background activity, when implemented.
- `last_command_failure`: user-visible command failure, when implemented.

Useful compact indicators:

- Number of free slots.
- Active playback indicator.
- Archive/freeing activity indicator.
- Disabled state when no archive candidate is available.

The strip should not maintain an independent truth for loop state. It may keep
local transient UI state, such as a pending auto-archive request, but must
resolve it against `looper.state`.

## Detailed Looper View

The detailed view exposes the full loop slot model. It can be a side panel,
drawer, modal, or inspector, but it should not be squeezed into the channel
strip.

The initial implementation should open the detailed view as a separate window.
This can be revisited after the workflow is better understood.

The detailed view should show:

- All 10 loop slots.
- Empty, ready, playing, archived, or pending state.
- Beat range: `start_beat..end_beat`.
- Length in beats and frames.
- Sample rate and channel count.
- BPM at cut time.
- Source: `ring`, `owned`, or archived/restored source once implemented.
- Active playhead for the currently playing slot.

Per-slot actions:

- Play.
- Stop.
- Archive.
- Cut into this slot.
- Replace this slot only after explicit user action.

Global controls:

- Mix.
- Default cut length.
- Archive status and archive folder availability.
- Transport/sync status.

The detailed view may expose explicit beat-range cutting:

```text
cut <start_beat> <end_beat> <slot>
```

This is intentionally not part of the compact strip.

## Scheduling

The channel strip should schedule commands on the beat grid. It should not try
to compensate or align implicitly beyond selecting the command beat. If a client
wants bar alignment or another musical alignment rule, it chooses the scheduled
beat accordingly.

The looper starts playback exactly at the scheduled beat. The UI should not
assume that playback starts at command submission time.

### First-Beat Quantize

The compact strip has a toggle for first-beat quantize. This affects only the
cut/play command beat selected by the UI.

The first-beat quantize setting is per channel.

The compact strip length selector is expressed in bars, not raw beats. The
initial implementation assumes 4/4 timing, so one bar is four beats. Supported
initial values:

- `1` bar = `4` beats
- `2` bars = `8` beats
- `4` bars = `16` beats
- `8` bars = `32` beats
- `16` bars = `64` beats

When first-beat quantize is off:

- The UI schedules cut/play at the next suitable beat.

When first-beat quantize is on:

- The UI schedules cut/play at the next loop-length phrase boundary, not merely
  the next bar start.
- The loop uses the selected bar length converted to beats.
- The native looper receives ordinary `cut` and `play` commands for the chosen
  beat.

For the initial 4/4 implementation, a bar is four beats:

```text
bar_index = floor(beat / 4)
```

The selected loop length defines a phrase grid. If the selected length is
`L` bars, the quantized target is the next boundary of that `L`-bar phrase
grid. For example, a 4-bar loop should start on 4-bar phrase boundaries, not on
every bar.

Using zero-based bar indexing, phrase starts are multiples of the selected loop
length:

```text
phrase_start_bar = n * selected_length_bars
```

For example, with a selected length of `4` bars, aligned starts are bars `0`,
`4`, `8`, `12`, and so on. If the current bar is `5`, the next aligned start is
the beginning of bar `8`.

The loop should then cover the selected number of bars starting with that bar.
For a 4-bar loop starting at bar `8`, the captured/played phrase covers bars
`8`, `9`, `10`, and `11`.

The UI owns this alignment rule. The looper does not infer bar starts
internally; it only executes the scheduled beat and beat-length command it
receives.

## Mix

The strip exposes a compact mix control for `mix`.

- `0`: dry input only.
- `1`: loop playback only.
- Values between `0` and `1`: dry/wet blend.

The detailed view may expose a more precise mix control.

If wet/dry summing causes audible coloration in real use, latency compensation
or short gain ramps may be added later. This is not part of the initial UI
contract.

## Data Source

The UI should use a looper client bound to the looper capture node. The client
listens to node param changes and parses:

- `mix`
- `commands`
- `looper.state`

On first access, if no param event has arrived yet, the client awaits the
node's initial params task. After that, it uses the latest parsed event data.

The UI should use the parsed looper params for display and command decisions.

## Failure Handling

For the first UI implementation, archive progress and archive failures may be
logged only. The compact strip should not grow additional error text for archive
jobs.

User-visible feedback is only required for immediate compact-control rejection,
such as a rejected pre/post-FX switch. In that case, use red button icons and
tooltips rather than visible text.
