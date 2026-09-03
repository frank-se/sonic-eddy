#pragma once

#include "midi_note_registry.hpp"

#include <cstddef>
#include <cstdint>

namespace midi_cube {

// Raw MIDI byte parser with running-status support, feeding note-on/off
// events straight into a NoteRegistry. No PipeWire/raylib includes - this is
// pure data/logic, called from the MIDI input stream's RT process callback.
class RunningStatusParser {
public:
  void feed(const uint8_t *data, size_t length, NoteRegistry &registry, double now);

private:
  uint8_t _running_status = 0;
};

} // namespace midi_cube
