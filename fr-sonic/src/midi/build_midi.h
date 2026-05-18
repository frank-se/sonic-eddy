#pragma once

#include "Messages.h"

#include <array>
#include <cstdint>

namespace midi {

std::optional<std::array<uint8_t, 3>> build_midi(const MessageV1 &message);

}
