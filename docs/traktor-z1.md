# Traktor Z1 Integration

The Traktor Z1 is not a standard MIDI controller. It requires drivers to
function as MIDI on Windows, and no Linux driver exists. It can be used directly
via raw HID access in userspace by opening `/dev/hidraw3` (or whichever hidraw
node the kernel assigns it).

## Input Report

The device sends 30-byte input reports with report ID `0x01`. All knob and fader
values are unsigned 16-bit little-endian integers. The final byte is a bitmask
for the three hardware buttons.

### Knobs and Faders

| Field         | Bytes | Control                    |
| ------------- | ----- | -------------------------- |
| `gain_l`      | 1–2   | Left channel gain knob     |
| `hi_l`        | 3–4   | Left channel hi EQ knob    |
| `mid_l`       | 5–6   | Left channel mid EQ knob   |
| `low_l`       | 7–8   | Left channel low EQ knob   |
| `filter_l`    | 9–10  | Left channel filter knob   |
| `gain_r`      | 11–12 | Right channel gain knob    |
| `hi_r`        | 13–14 | Right channel hi EQ knob   |
| `mid_r`       | 15–16 | Right channel mid EQ knob  |
| `low_r`       | 17–18 | Right channel low EQ knob  |
| `filter_r`    | 19–20 | Right channel filter knob  |
| `cue_mix`     | 21–22 | Headphone cue mix knob     |
| `fader_l`     | 23–24 | Left channel volume fader  |
| `fader_r`     | 25–26 | Right channel volume fader |
| `cross_fader` | 27–28 | Crossfader                 |

### Button States

Byte 29 is a bitmask. Each button has an assigned bit; the byte is the sum of
all currently pressed buttons.

| Bit value | Button              |
| --------- | ------------------- |
| `0x02`    | Mode button   |
| `0x04`    | on_button_l   |
| `0x08`    | on_button_r   |

Examples: `0x06` = Mode + Left filter, `0x0e` = all three pressed.

## Output Report

To control the LEDs send a 22-byte output report (report ID `0x80` followed by
21 payload bytes) using `write()` on the hidraw file descriptor. All bytes
default to `0x00` (off). Brightness values range from `0x00` (off) to `0xff`
(full).

### VU Meters

Each channel has a 7-segment bargraph display. Each segment is individually
addressable.

| Payload byte | LED                   |
| ------------ | --------------------- |
| 0–6          | Left VU segments 0–6  |
| 7–13         | Right VU segments 0–6 |

Segment 0 is the bottom of the bargraph, segment 6 is the top.

### Buttons

| Payload byte | Control                        |
| ------------ | ------------------------------ |
| 14           | Cue A button brightness *(LED only, no corresponding input)*  |
| 15           | Cue B button brightness *(LED only, no corresponding input)*  |
| 16           | on_button_l brightness  |
| 17           | on_button_l color       |
| 18           | Mode button brightness  |
| 19           | on_button_r brightness  |
| 20           | on_button_r color       |

The color bytes (17 and 20) are not yet fully understood. Use specific known
values rather than trying to compute a color.

## Application Mapping

- `fader_l` => Layer A master channel volume (post-fx looper capture node)
- `fader_r` => Layer B master channel volume (post-fx looper capture node)
- `on_button_l` => Change params section for the filter controlled by the left side
- `on_button_r` => Change params section for the filter controlled by the right side
- `low_l` => 4th parameter of the selected filter section on the left side
- `low_r` => 4th parameter of the selected filter section on the right side
- `mid_l` => 3rd parameter of the selected filter section on the left side
- `mid_r` => 3rd parameter of the selected filter section on the right side
- `hi_l` => 2nd parameter of the selected filter section on the left side
- `hi_r` => 2nd parameter of the selected filter section on the right side
- `gain_l` => 1st parameter of the selected filter section on the left side
- `gain_r` => 1st parameter of the selected filter section on the right side
- `mode_button` => *(not yet decided)*
- `filter_l` => *(not yet decided)*
- `filter_r` => *(not yet decided)*

The following controls require graph changes (new crossfade/cue nodes) and are
deferred to a later iteration:

- `cross_fader` => Fade between layer A and layer B (needs a new crossfade node)
- `cue_mix` => Fade between layer A and layer B on the cue node (needs a new node
  that merges both masters)

## Code Examples

### Reading Input Reports

```cpp
int fd = open("/dev/hidraw3", O_RDONLY);

std::array<std::uint8_t, 30> buffer = {};
traktor_z1_message report{};

while (read(fd, buffer.data(), buffer.size()) == 30) {
  if (buffer[0] != 1) continue;

  report.gain_l      = (buffer[2]  << 8) | buffer[1];
  report.hi_l        = (buffer[4]  << 8) | buffer[3];
  report.mid_l       = (buffer[6]  << 8) | buffer[5];
  report.low_l       = (buffer[8]  << 8) | buffer[7];
  report.filter_l    = (buffer[10] << 8) | buffer[9];
  report.gain_r      = (buffer[12] << 8) | buffer[11];
  report.hi_r        = (buffer[14] << 8) | buffer[13];
  report.mid_r       = (buffer[16] << 8) | buffer[15];
  report.low_r       = (buffer[18] << 8) | buffer[17];
  report.filter_r    = (buffer[20] << 8) | buffer[19];
  report.cue_mix     = (buffer[22] << 8) | buffer[21];
  report.fader_l     = (buffer[24] << 8) | buffer[23];
  report.fader_r     = (buffer[26] << 8) | buffer[25];
  report.cross_fader = (buffer[28] << 8) | buffer[27];
  report.buttons     = buffer[29];
}
```

### Writing Output Reports

```cpp
int fd = open("/dev/hidraw3", O_RDWR);

std::array<std::uint8_t, 22> out = {};
out[0] = 0x80;  // report ID

// Fill left VU bargraph to 4 segments
out[1] = 0xff;
out[2] = 0xff;
out[3] = 0xff;
out[4] = 0xff;

// Light on_button_l
out[17] = 0xff;  // brightness
out[18] = 0x0f;  // color

write(fd, out.data(), out.size());
```
