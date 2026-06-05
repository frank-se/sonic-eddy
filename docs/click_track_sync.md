# Click Track Sync

The click track sync translates the Sonic Eddy beat sync to sync signals
typically used by eurorack, some drum machines, and other audio equipment.

The implementation is very similar to midi sync, with the biggest difference
that ppqn can be defined per click track converter, while it is given for midi.

## The click track converter

The click track converter creates three playback nodes with mono outputs. The
first node, the click node, plays a click track, it creates short audio pulses
every beat, or configured interval.

It has a `pulses_per_quarter_note` setting, which defines how the click track is
interpolated between quarter-note beats. The value must be a positive integer.
The initial UI supports values from `1` to `96`. A value of `24` PPQN produces
one pulse for every MIDI clock message.

PPQN, pulse length, and pulse amplitude are fixed when a converter is created.
Changing these settings requires creating a new converter.

The click track is produced when the transport is running.

The second output creates a reset signal, a single pulse, when playback starts.

The third output creates a run signal. It is high at the configured pulse
amplitude while the transport is `start_scheduled` at or after the scheduled
start beat, or `playing`, and low while the transport is stopped. Scheduled
start and stop transitions occur at their scheduled beat sample.

## Timing and transport

Pulse generation runs in the native PipeWire process callback. The converter
uses `se_sync_get_beats` to translate the Sonic Eddy beat schedule into sample
positions for the current processing cycle. C# timers or UI-thread scheduling
must not be used for pulse generation.

Clock pulses are interpolated between consecutive scheduled quarter-note beats,
using the same timing model as the MIDI sync sender:

```text
pulse_sample[i] =
    beat_sample +
    i * (next_beat_sample - beat_sample) / pulses_per_quarter_note
```

Tempo changes require no separate adjustment because the scheduled beat sample
positions already contain the updated timing.

When the transport state is `start_scheduled`, the first clock pulse and the
reset pulse are emitted at the scheduled start beat. When the transport later
changes to `playing` for the same start beat, the converter must not emit a
second reset pulse or repeat the first clock pulse. Each transport start beat is
handled once.

No click pulses are produced while transport is stopped.

Converter creation is only allowed while transport is stopped. The UI disables
creation while playback is running or scheduled to start.

If a scheduled transport start is cancelled or replaced, the converter follows
the latest published sync state and schedule. It emits only the pulses required
by the current state. A stop followed by a later restart is a new transport
start and emits a new reset pulse and clock pulses.

## Pulse shape

Both the click and reset outputs use the same rectangular pulse shape:

- Low level: `0.0`
- High level: `0.75`
- Default pulse length: `5 ms`
- Configurable pulse length range: `0.1 ms` to `20 ms`

The native converter calculates the pulse length in samples from the current
sample rate:

```text
pulse_samples = sample_rate * pulse_length_ms / 1000
```

The effective pulse length is clamped to at most half of the interval until the
next pulse. This guarantees a distinct low period between pulses, including at
high BPM and PPQN settings.

## Connection management

Connection management is similar to midi sync, we manage the connections
manually, in order to be able to share click tracks. We add a click sync UI that
allows creation of click track converters with different settings, and lets us
select the capture ports to which this should be connected.

The connections for all three nodes need to be freely selectable.

Each converter output supports independent fan-out:

- The click output can connect to zero or more capture ports.
- The reset output can connect to zero or more capture ports.
- The run output can connect to zero or more capture ports.

The same converter can therefore share its generated click, reset, and run
signals with multiple external devices. All three target sets are managed
separately.

## Node identity

Each converter uses its stable converter ID in its PipeWire node names:

```text
se.click_sync.<converter-id>.click
se.click_sync.<converter-id>.reset
se.click_sync.<converter-id>.run
```

All three nodes expose one mono output port. Their display names may use the
converter's user-facing name, but discovery and connection management use the
stable node names above.

## Persistence

Settings are persisted, similar to midi sync, but take different click tracks
into account.

For each converter, persist:

- Stable converter ID
- Display name
- Pulses per quarter note
- Pulse length in milliseconds
- Pulse amplitude
- Click-output target ports
- Reset-output target ports
- Run-output target ports

Target ports are identified by stable node name plus port name and alias, not by
PipeWire object ID or object serial. If a configured port is unavailable, retain
the configuration and create the link when the port appears.

PipeWire link IDs and converter node object IDs are runtime state and are not
persisted.

## Hardware requirements

The voltage produced by a pulse depends on the audio interface. An amplitude of
`0.75` is a digital sample value, not a guaranteed output voltage.

Reliable Eurorack clock and reset signals require outputs that produce a
suitable trigger voltage. DC-coupled outputs are generally required for
reliable gate and trigger behavior. Users are responsible for verifying that
their audio interface and connected equipment are electrically compatible.
