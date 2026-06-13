# Duck Section

Every group channel has a ducker placed after the filter section and before the
send section in the signal flow.

The ducker has a MIDI capture port, an audio capture port, and an audio playback
port. It takes signal in the audio capture port and plays it back through the
audio playback port. When it receives a MIDI note-on, it ducks the signal by
applying an inverted AR envelope to the gain. It provides the parameters attack,
release, depth, attack shape, and release shape to control the ducking
behaviour, and a bypass parameter to disable processing.

## AR Envelope

The envelope is applied in inverse: when a note-on is received the gain falls
toward zero (scaled by depth), and it recovers to unity when the note is
released.

- **Attack**: time for the gain to fall from unity to the target level after a
  note-on.
- **Release**: time for the gain to return to unity after a note-off.

## Envelope Shapes

Both attack and release have an independently selectable curve shape:

- **Linear**: gain changes at a constant rate.
- **Exponential**: gain changes quickly at first, then slows as it approaches
  the target.
- **Logarithmic**: gain changes slowly at first, then accelerates toward the
  target.

## Depth and Note Number

The **depth** parameter (0.0–1.0) sets the maximum gain reduction applied when
the lowest MIDI note is received. The actual applied depth scales linearly with
note number so that lower notes produce more ducking:

```
applied_depth = ((127 - note) / 127) * depth
```

At note 127 no ducking occurs regardless of depth; at note 0 the full depth
value is applied.

## Velocity and Attack

Note velocity shortens the attack time: higher velocity produces a faster
(shorter) attack. The relationship is linear:

```
effective_attack = attack * (1 - velocity / 127)
```

At velocity 127 the attack is instantaneous; at velocity 0 the configured attack
time is used in full.

## Bypass

Bypass is a soft bypass: the gain is held at unity and the MIDI port still
receives events, but no envelope processing is applied. The audio path remains
connected.

## User Interface

The duck section front panel shows only a bypass toggle button and a detail
button that opens a detail window, similar to the looper detail window.

The detail window provides value sliders for attack, release, and depth, and
combobox selectors for the attack and release shapes.
