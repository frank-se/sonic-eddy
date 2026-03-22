#include "midi/build_ump.h"

#include "midi/build_midi.h"

#include <iostream>

midi::UmpBytes midi::build_ump(const Message &message) {
  UmpBytes result;

  std::visit(
      [&result](auto &&arg) {
        using T = std::decay_t<decltype(arg)>;
        if constexpr (std::is_same_v<T, NoteOnV2>) {
          const uint16_t status = static_cast<uint16_t>(4) << 12 |
                                  static_cast<uint16_t>(arg.group) << 8 |
                                  static_cast<uint16_t>(0b1001) << 4 |
                                  static_cast<uint16_t>(arg.channel);
          const uint16_t note_number = static_cast<uint16_t>(arg.note_number)
                                       << 8;

          std::array<uint32_t, 2> integers{};
          integers[0] = static_cast<uint16_t>(status) << 16 |
                        static_cast<uint16_t>(note_number);

          integers[1] = static_cast<uint64_t>(arg.velocity) << 16;
          result = integers;
        } else if constexpr (std::is_same_v<T, NoteOffV2>) {
          const uint16_t status = static_cast<uint16_t>(4) << 12 |
                                  static_cast<uint16_t>(arg.group) << 8 |
                                  static_cast<uint16_t>(0b1000) << 4 |
                                  static_cast<uint16_t>(arg.channel);
          const uint16_t note_number = static_cast<uint16_t>(arg.note_number)
                                       << 8;

          std::array<uint32_t, 2> integers{};
          integers[0] = static_cast<uint16_t>(status) << 16 |
                        static_cast<uint16_t>(note_number);

          integers[1] = static_cast<uint64_t>(arg.velocity) << 16;
          result = integers;
        } else if constexpr (std::is_same_v<T, ControlChangeV2>) {
          const uint16_t status =
              4 << 12 | arg.group << 8 | 0b1011 << 4 | arg.channel;
          const uint16_t index = arg.index << 8;

          std::array<uint32_t, 2> integers{};
          integers[0] = status << 16 | index;
          integers[1] = arg.value;
          result = integers;
        } else if constexpr (std::is_same_v<T, NoteOnV1>) {
          auto midi_v1 = build_midi(arg);
          if (!midi_v1)
            return;

          uint32_t integer = 2 << 28 | (*midi_v1)[0] << 16 |
                             (*midi_v1)[1] << 8 | (*midi_v1)[2];

          result = integer;
        } else if constexpr (std::is_same_v<T, NoteOffV1>) {
          auto midi_v1 = build_midi(arg);
          if (!midi_v1)
            return;

          uint32_t integer = 2 << 28 | (*midi_v1)[0] << 16 |
                             (*midi_v1)[1] << 8 | (*midi_v1)[2];

          result = integer;
        } else if constexpr (std::is_same_v<T, ControlChangeV1>) {
          auto midi_v1 = build_midi(arg);
          if (!midi_v1)
            return;

          uint32_t integer = 2 << 28 | (*midi_v1)[0] << 16 |
                             (*midi_v1)[1] << 8 | (*midi_v1)[2];

          result = integer;
        }
      },
      message);

  return result;
}
