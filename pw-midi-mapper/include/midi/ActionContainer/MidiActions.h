#pragma once

#include "midi/Messages.h"

#include <cstdint>
#include <functional>

namespace midi::action_container {

struct NoteOnMidiAction {
  size_t layer_id;
  uint8_t group;
  uint8_t channel;
  uint8_t note_number;
  std::function<void(const NoteOnMessages &)> behavior;
};

struct NoteOffMidiAction {
  size_t layer_id;
  uint8_t group;
  uint8_t channel;
  uint8_t note_number;
  std::function<void(const NoteOffMessages &)> behavior;
};

struct ControlChangeMidiAction {
  size_t layer_id;
  uint8_t group;
  uint8_t channel;
  uint8_t index;
  std::function<void(const ControlChangeMessages &)> behavior;
};

using MidiAction =
    std::variant<NoteOnMidiAction, NoteOffMidiAction, ControlChangeMidiAction>;
} // namespace midi::action_container
