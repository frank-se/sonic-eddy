#pragma once

#include "logging/log.h"
#include "pw_utils/SetParamsData.h"

#include <array>
#include <spa/param/props.h>
#include <spa/pod/builder.h>

namespace pw_utils {

inline void fill_set_value_pod(std::array<float, 2> gains,
                               SetParamsData &props_data) {
  logging::log<logging::LogLevel::Trace>("pw_utils::fill_set_value_pod");

  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, &props_data.buffer, sizeof(props_data.buffer));

  spa_pod_frame frame{};
  spa_pod_builder_push_object(&builder, &frame, SPA_TYPE_OBJECT_Props,
                              SPA_PARAM_Props);

  spa_pod_builder_prop(&builder, SPA_PROP_channelVolumes, 0);

  spa_pod_frame array_frame{};
  spa_pod_builder_push_array(&builder, &array_frame);

  spa_pod_builder_float(&builder, gains[0]);
  spa_pod_builder_float(&builder, gains[1]);

  spa_pod_builder_pop(&builder, &array_frame);

  props_data.pod =
      static_cast<spa_pod *>(spa_pod_builder_pop(&builder, &frame));
}

} // namespace pw_utils