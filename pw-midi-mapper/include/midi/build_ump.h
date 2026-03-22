#pragma once
#include "Messages.h"

#include <cstdint>

namespace midi {

using UmpBytes = std::variant<std::monostate, uint32_t, std::array<uint32_t, 2>>;

UmpBytes build_ump(const Message &message);

}