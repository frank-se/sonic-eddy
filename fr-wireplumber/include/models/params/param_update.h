#pragma once

#include <spa/pod/pod.h>

#include "param_type.h"

namespace models::params {
struct ParamUpdate {
  size_t count;
  const char **keys;
  ParamType *types;
  uint64_t *values;
};

void fill_params_from_pod(const spa_pod *params_pod, ParamUpdate &params);

[[nodiscard]] size_t count_params(const spa_pod *params_pod);

void params_data_delete(ParamUpdate &param_update);

void fill_params(ParamUpdate &params, const spa_pod *params_pod);
} // namespace models::params
