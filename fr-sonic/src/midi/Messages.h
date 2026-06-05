#pragma once
#include <cstdint>
#include <array>
#include <iosfwd>
#include <ostream>
#include <variant>
#include <vector>

namespace midi {

struct ControlChangeV1 {
  uint8_t channel;
  uint8_t index;
  uint8_t value;
};

struct NoteOnV1 {
  uint8_t channel;
  uint8_t note_number;
  uint8_t velocity;
};

struct NoteOffV1 {
  uint8_t channel;
  uint8_t note_number;
  uint8_t velocity;
};

struct ControlChangeV2 {
  uint8_t group;
  uint8_t channel;
  uint8_t index;
  uint32_t value;
};

struct NoteOnV2 {
  uint8_t group;
  uint8_t channel;
  uint8_t note_number;
  uint16_t velocity;
};

struct NoteOffV2 {
  uint8_t group;
  uint8_t channel;
  uint8_t note_number;
  uint16_t velocity;
};

struct RawUmpMessage {
  std::array<uint32_t, 4> words{};
  uint8_t word_count{};
};

using Message = std::variant<ControlChangeV1, NoteOnV1, NoteOffV1,
                             ControlChangeV2, NoteOnV2, NoteOffV2,
                             RawUmpMessage>;

using MessageV1 = std::variant<ControlChangeV1, NoteOnV1, NoteOffV1>;

using MessageV2 = std::variant<ControlChangeV2, NoteOnV2, NoteOffV2>;

using ControlChangeMessages = std::variant<ControlChangeV1, ControlChangeV2>;

using NoteOnMessages = std::variant<NoteOnV1, NoteOnV2>;

using NoteOffMessages = std::variant<NoteOffV1, NoteOffV2>;

inline double normalized_cc_value(ControlChangeMessages message) {
  double result = 0.0;
  std::visit(
      [&result](auto &&arg) {
        using T = std::decay_t<decltype(arg)>;
        if constexpr (std::is_same_v<T, ControlChangeV1>) {
          constexpr double max = 127.0;
          const double value = arg.value;
          result = value / max;
        } else if constexpr (std::is_same_v<T, ControlChangeV2>) {
          constexpr double max = std::numeric_limits<uint32_t>::max();
          const double value = arg.value;
          result = value / max;
        }
      },
      message);

  return result;
}

} // namespace midi

inline std::ostream &operator<<(std::ostream &lhs,
                                const std::optional<midi::Message> &message) {
  if (message) {
    auto midi_message = message.value();
    std::visit(
        [&lhs](auto &&arg) {
          using T = std::decay_t<decltype(arg)>;
          if constexpr (std::is_same_v<T, midi::ControlChangeV2>) {
            lhs << "Control Change V2" << std::endl;
            lhs << "Channel " << static_cast<int>(arg.channel) << std::endl;
            lhs << "Control Change " << static_cast<int>(arg.index)
                << std::endl;
            lhs << "Value " << arg.value << std::endl;
          } else if constexpr (std::is_same_v<T, midi::NoteOnV2>) {
            lhs << "Note On V2" << std::endl;
            lhs << "Channel " << static_cast<int>(arg.channel) << std::endl;
            lhs << "Note On " << static_cast<int>(arg.note_number) << std::endl;
            lhs << "Velocity " << arg.velocity << std::endl;
          } else if constexpr (std::is_same_v<T, midi::NoteOffV2>) {
            lhs << "Note Off V2" << std::endl;
            lhs << "Channel " << static_cast<int>(arg.channel) << std::endl;
            lhs << "Note Off " << static_cast<int>(arg.note_number)
                << std::endl;
            lhs << "Velocity " << arg.velocity << std::endl;
          } else if constexpr (std::is_same_v<T, midi::ControlChangeV1>) {
            lhs << "Control Change V1" << std::endl;
            lhs << "Channel " << static_cast<int>(arg.channel) << std::endl;
            lhs << "Control Change " << static_cast<int>(arg.index)
                << std::endl;
            lhs << "Value " << static_cast<int>(arg.value) << std::endl;
          } else if constexpr (std::is_same_v<T, midi::NoteOnV1>) {
            lhs << "Note On V1" << std::endl;
            lhs << "Channel " << static_cast<int>(arg.channel) << std::endl;
            lhs << "Note On " << static_cast<int>(arg.note_number) << std::endl;
            lhs << "Velocity " << static_cast<int>(arg.velocity) << std::endl;
          } else if constexpr (std::is_same_v<T, midi::NoteOffV1>) {
            lhs << "Note Off V1" << std::endl;
            lhs << "Channel " << static_cast<int>(arg.channel) << std::endl;
            lhs << "Note Off " << static_cast<int>(arg.note_number)
                << std::endl;
            lhs << "Velocity " << static_cast<int>(arg.velocity) << std::endl;
          } else if constexpr (std::is_same_v<T, midi::RawUmpMessage>) {
            lhs << "Raw UMP" << std::endl;
            lhs << "Word count " << static_cast<int>(arg.word_count)
                << std::endl;
          }
        },
        midi_message);
  }

  return lhs;
}
