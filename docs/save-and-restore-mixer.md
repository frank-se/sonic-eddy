# Save and Restore Mixer

Menu entries in file menu:

- _Save_: Save mixer, if it doesn't have a name yet, ask for one
- _Load_: Give list of mixers, user can load one
- _Save As_: Save with new name

Each mixer has an internal ID. _Save_ updates the mixer with that ID, while
_Save As_ creates a new mixer with a new ID. Names are only used for display and
do not need to be unique.

Loading is only allowed while playback is stopped. Loading does not create a
new mixer or replace its PipeWire resources. It applies the stored
configuration to the existing mixer.

Data saved:

All channel settings are stored separately for each mixer layer.

For each normal channel:

- Audio from
- Audio to
- Filter graph and setting
- Pan and volume
- Sends
- Gain/Trim
- Looper settings

For each group channel:

- Audio to
- Filter graph and setting
- Pan and volume
- Sends
- Looper settings

For each master channel:

- Audio to
- Filter graph and setting
- Pan and volume
- Looper settings

For each return channel

- Filter graph and settings
- Pan and volume

Filter graph settings use the same format as filter chain presets: the filter
graph ID and parameter values identified by fully qualified parameter name and
typed value.

Looper settings consist of:

- Selected pre-FX or post-FX looper
- Mix
- Bar length
- Quantized state

Recorded loops, active playback, recording state, scheduled commands, failures,
and archive jobs are not stored.

Audio routing targets are stored by stable node name, never by PipeWire object
ID or object serial. If a configured node is unavailable while loading, retain
the requested route and apply it when the node appears. A temporary fallback
must not overwrite the stored route.

For the main and cue crossfaders:

- Position
- Shape
- Mode

For the cue channel:

- Audio to
- Pan and volume

We do not store anything for input and output channels, as those are controlled
by the operating system/desktop environment. Mixer routes targeting those
channels are still stored by node name.
