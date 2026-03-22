#pragma once

#include <pipewire/pipewire.h>
#include <spa/pod/pod.h>

#include <cstdint>

namespace pw_utils {

struct SetParamsData {
  constexpr static size_t size = 128;
  std::uint8_t buffer[size]{};
  spa_pod *pod = nullptr;
  pw_node *node = nullptr;
};

} // namespace pw_utils
