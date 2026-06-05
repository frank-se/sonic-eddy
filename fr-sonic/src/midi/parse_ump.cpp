#include "midi/parse_ump.h"

#include "midi/parse_midi.h"

#include <iostream>
#include <ostream>

std::optional<midi::Message> midi::parse_ump(const void *data,
                                             const uint32_t length) {
  if (data == nullptr)
    return std::nullopt;

  auto *d = static_cast<const uint32_t *>(data);

  const uint8_t message_type = (d[0] >> 28) & 0xf;

  if (message_type == MIDI_MESSAGE_TYPE_8_BYTE_DATA ||
      message_type == MIDI_MESSAGE_TYPE_MIDI_2_16_BYTE_DATA) {
    if (length % 4 != 0 || length > 16)
      return std::nullopt;

    RawUmpMessage message{};
    message.word_count = static_cast<uint8_t>(length / 4);
    for (size_t i = 0; i < message.word_count; i++)
      message.words[i] = d[i];

    return message;
  }

  if (message_type == MIDI_MESSAGE_TYPE_MIDI_1_CHANNEL_VOICE) {
    if (length != 4)
      return std::nullopt;

    std::array<uint8_t, 3> midi_bytes{
        static_cast<uint8_t>((d[0] >> 16) & 0xff),
        static_cast<uint8_t>((d[0] >> 8) & 0xff),
        static_cast<uint8_t>(d[0] & 0xff),
    };

    return parse_midi(midi_bytes.data(), midi_bytes.size());
  }

  if (message_type != MIDI_MESSAGE_TYPE_MIDI_2_CHANNEL_VOICE) {
    std::cerr << "Unsupported message type: " << message_type << std::endl;
    return std::nullopt;
  }

  uint8_t status = d[0] >> 16;
  if (status >= 0xb0 && status <= 0xbf) {
    return ControlChangeV2{.channel = static_cast<unsigned char>((status & 0x0f)),
                         .index = static_cast<unsigned char>(d[0] >> 8 & 0x7f),
                         .value = d[1]};
  }

  if (status >= 0x80 && status <= 0x8f) {
    return NoteOffV2{.channel = static_cast<unsigned char>((status & 0x0f)),
                   .note_number = static_cast<unsigned char>(d[0] >> 8 & 0x7f),
                   .velocity = static_cast<uint16_t>((d[1] >> 16) & 0xffff)};
  }

  if (status >= 0x90 && status <= 0x9f) {
    return NoteOnV2{.channel = static_cast<unsigned char>((status & 0x0f)),
                  .note_number = static_cast<unsigned char>(d[0] >> 8 & 0x7f),
                  .velocity = static_cast<uint16_t>((d[1] >> 16) & 0xffff)};
  }

  return std::nullopt;
}
