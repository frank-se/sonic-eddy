#pragma once

#include "midi/Messages.h"

#include <optional>

namespace midi {

std::optional<Message> parse_midi(const uint8_t *data, const size_t length);

}