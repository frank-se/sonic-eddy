#pragma once

#include "SetParamsData.h"
#include "fill_set_value_pod.h"
#include "logging/log.h"

#include <array>
#include <pipewire/pipewire.h>
#include <spa/debug/pod.h>

namespace pw_utils {

inline void set_pw_node_volume(pw_main_loop *loop, pw_node *node,
                               std::array<float, 2> volume) {
  logging::log<logging::LogLevel::Trace>(
      "pw_utils::set_pw_node_volume to left {} and right {}", volume[0],
      volume[1]);

  auto *props_data = new SetParamsData{.node = node};

  fill_set_value_pod(volume, *props_data);

  pw_loop_invoke(
      pw_main_loop_get_loop(loop),
      [](spa_loop *loop, bool async, std::uint32_t seq, const void *data,
         size_t size, void *user_data) {
        auto set_props_data = static_cast<SetParamsData *>(user_data);

        logging::log<logging::LogLevel::Debug>(
            "Calling pw_node_set_param in pipewire loop");

        pw_node_set_param(set_props_data->node, SPA_PARAM_Props, 0,
                          set_props_data->pod);

        delete set_props_data;
        return 0;
      },
      0, nullptr, 0, false, props_data);
}

} // namespace pw_utils
