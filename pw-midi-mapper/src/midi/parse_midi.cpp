#include "midi/parse_midi.h"

#include <iostream>
#include <ostream>

std::optional<midi::Message> midi::parse_midi(const uint8_t *data,
                                              const size_t length) {
  if (length != 3 && length != 2)
    return std::nullopt;

  const auto status = data[0];
  const auto channel =
      static_cast<uint8_t>(data[0] & static_cast<uint8_t>(0b00001111));

  if (status >> 4 == 0b00001000) {
    if (length != 3)
      return std::nullopt;
    return midi::NoteOffV1{
        .channel = channel,
        .note_number = data[1],
        .velocity = data[2],
    };
  } else if (status >> 4 == 0b00001001) {
    if (length != 3)
      return std::nullopt;
    return midi::NoteOnV1{
        .channel = channel,
        .note_number = data[1],
        .velocity = data[2],
    };
  } else if (status >> 4 == 0b00001011) {
    if (length != 3)
      return std::nullopt;
    return midi::ControlChangeV1{
        .channel = channel,
        .index = data[1],
        .value = data[2],
    };
  } else if (status >> 4 == 0b00001100) {
    std::cout << "Program change" << std::endl;
  }

  return std::nullopt;
}
