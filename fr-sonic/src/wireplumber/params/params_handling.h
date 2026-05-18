#pragma once

#include <cstddef>
#include <cstdint>
#include <memory>

#include <wp/wp.h>

#include "models/params/param_type.h"

class Core;

namespace params::params_handling {
struct set_params_data {
  constexpr static size_t size = 4096;
  std::uint8_t buffer[size]{};
  WpSpaPod *pod = nullptr;
  WpPipewireObject *proxy = nullptr;
};

void set_params(uint64_t object_id, char **keys,
                models::params::ParamType *param_types, uint64_t *values,
                size_t count,
                const std::shared_ptr<Core> &wireplumber_thread);

void build_set_params_pod(char **keys,
                          const models::params::ParamType *param_types,
                          uint64_t *values, size_t count,
                          set_params_data &data);
} // namespace params::params_handling
