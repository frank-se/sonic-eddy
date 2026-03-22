#pragma once

#include "midi/Messages.h"

#include <optional>

namespace midi {

constexpr uint8_t MIDI_MESSAGE_TYPE_UTILITY = 0x0;
constexpr uint8_t MIDI_MESSAGE_TYPE_COMMON_OR_REAL_TIME = 0x1;
constexpr uint8_t MIDI_MESSAGE_TYPE_MIDI_1_CHANNEL_VOICE = 0x2;
constexpr uint8_t MIDI_MESSAGE_TYPE_8_BYTE_DATA = 0x3;
constexpr uint8_t MIDI_MESSAGE_TYPE_MIDI_2_CHANNEL_VOICE = 0x4;
constexpr uint8_t MIDI_MESSAGE_TYPE_MIDI_2_16_BYTE_DATA = 0x5;

std::optional<Message> parse_ump(const void *data);

}
