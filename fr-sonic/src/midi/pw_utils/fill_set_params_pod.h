#pragma once

#include "logging/log.h"
#include "pw_utils/SetParamsData.h"

#include <spa/param/props.h>
#include <spa/pod/builder.h>
#include <string>

namespace pw_utils {

inline void fill_set_params_pod(const std::string &name, float value,
                                SetParamsData &props_data) {

  logging::log<logging::LogLevel::Trace>("pw_utils::fill_set_params_pod");

  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, props_data.buffer,
                       pw_utils::SetParamsData::size);

  spa_pod_frame object_frame{};
  spa_pod_builder_push_object(&builder, &object_frame, SPA_TYPE_OBJECT_Props,
                              SPA_PARAM_Props);
  spa_pod_builder_prop(&builder, SPA_PROP_params, 0);

  spa_pod_frame struct_frame{};
  spa_pod_builder_push_struct(&builder, &struct_frame);
  spa_pod_builder_string(&builder, name.c_str());
  spa_pod_builder_float(&builder, value);

  spa_pod_builder_pop(&builder, &struct_frame);
  props_data.pod =
      static_cast<spa_pod *>(spa_pod_builder_pop(&builder, &object_frame));
}

} // namespace pw_utils
