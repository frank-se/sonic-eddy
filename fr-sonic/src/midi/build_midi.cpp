#include "midi/build_midi.h"

std::optional<std::array<uint8_t, 3>>
midi::build_midi(const MessageV1 &message) {
  std::array<uint8_t, 3> result{};

  std::visit(
      [&result](auto &&arg) {
        using T = std::decay_t<decltype(arg)>;
        if constexpr (std::is_same_v<T, ControlChangeV1>) {
          result[0] = 0b10110000 | arg.channel;
          result[1] = arg.index;
          result[2] = arg.value;
        } else if constexpr (std::is_same_v<T, NoteOnV1>) {
          result[0] = 0b10010000 | arg.channel;
          result[1] = arg.note_number;
          result[2] = arg.velocity;
        } else if constexpr (std::is_same_v<T, NoteOffV1>) {
          result[0] = 0b10000000 | arg.channel;
          result[1] = arg.note_number;
          result[2] = arg.velocity;
        }
      },
      message);

  return result;
}
