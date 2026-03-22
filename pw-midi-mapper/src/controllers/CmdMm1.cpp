#include <controllers/CmdMm1.h>

#include "logging/log.h"

#include <iostream>

void controllers::CmdMm1::process(const midi::Message &message) {
  logging::log<logging::LogLevel::Trace>("CmdMm1::process");

  std::cout << message << std::endl;

  std::visit(
      [this](auto &&arg) {
        using T = std::decay_t<decltype(arg)>;
        if constexpr (std::is_same_v<T, midi::NoteOnV1>) {
        } else if constexpr (std::is_same_v<T, midi::NoteOnV2>) {
        } else if constexpr (std::is_same_v<T, midi::ControlChangeV1>) {
        } else if constexpr (std::is_same_v<T, midi::ControlChangeV2>) {
        }
      },
      message);

  _feedback_channel->push(midi::ControlChangeV1{
      .channel = 4,
      .index = 80,
      .value = 53,
  });

  _feedback_channel->push(midi::ControlChangeV1{
      .channel = 4,
      .index = 81,
      .value = 60,
  });

  _feedback_channel->push(midi::NoteOnV1{
    .channel = 4,
    .note_number = 16,
    .velocity = 1,
  });
}
