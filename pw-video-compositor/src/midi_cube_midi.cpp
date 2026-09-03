#include "midi_cube_midi.hpp"

namespace midi_cube {

void RunningStatusParser::feed(const uint8_t *data, size_t length,
                               NoteRegistry &registry, double now) {
  size_t i = 0;
  while (i < length) {
    const uint8_t byte = data[i];

    if (byte >= 0xF8) {
      // System realtime (clock/start/stop/...): single byte, never affects
      // running status, may appear spliced in the middle of another message.
      ++i;
      continue;
    }

    uint8_t status;
    if (byte & 0x80) {
      status = byte;
      ++i;
      if (status < 0xF0)
        _running_status = status; // channel voice message - persists
      else
        _running_status = 0; // system common message clears running status
    } else {
      status = _running_status;
      if (status == 0) {
        ++i; // no running status to fall back on - drop this stray byte
        continue;
      }
      // don't advance i - this byte is the first data byte of the
      // running-status message.
    }

    const uint8_t message_type = status & 0xF0;
    const uint8_t channel = status & 0x0F;

    size_t data_bytes = 0;
    bool is_note_message = false;
    switch (message_type) {
    case 0x80: // note off
    case 0x90: // note on
      data_bytes = 2;
      is_note_message = true;
      break;
    case 0xA0: // poly aftertouch
    case 0xB0: // control change
    case 0xE0: // pitch bend
      data_bytes = 2;
      break;
    case 0xC0: // program change
    case 0xD0: // channel aftertouch
      data_bytes = 1;
      break;
    default:
      // System common (0xF0-0xF7, incl. sysex) - not parsed in v1.
      data_bytes = 0;
      break;
    }

    if (i + data_bytes > length)
      break; // incomplete message at buffer end - drop rather than block

    if (is_note_message) {
      const uint8_t note_number = data[i];
      const uint8_t velocity = data[i + 1];
      if (message_type == 0x90 && velocity > 0)
        registry.note_on(channel, note_number, velocity, now);
      else
        registry.note_off(channel, note_number, now); // incl. note-on w/ velocity 0
    }
    i += data_bytes;
  }
}

} // namespace midi_cube
