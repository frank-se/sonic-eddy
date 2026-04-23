#pragma once

#include "SetParamsData.h"
#include "fill_set_params_pod.h"
#include "logging/log.h"

#include <pipewire/pipewire.h>

#include <spa/debug/pod.h>
#include <string>

namespace pw_utils {

inline void set_pw_node_param(pw_main_loop *loop, pw_node *node,
                              const std::string &param_name, float value) {
  logging::log<logging::LogLevel::Trace>("pw_utils::set_pw_node_param");

  const auto data = new SetParamsData{.node = node};

  fill_set_params_pod(param_name, value, *data);

  pw_loop_invoke(
      pw_main_loop_get_loop(loop),
      [](spa_loop *loop, bool async, std::uint32_t seq, const void *data,
         size_t size, void *user_data) {
        auto set_params_data =
            static_cast<pw_utils::SetParamsData *>(user_data);

        logging::log<logging::LogLevel::Debug>(
            "Calling pw_node_set_param in pipewire loop");

        pw_node_set_param(set_params_data->node, SPA_PARAM_Props, 0,
                          set_params_data->pod);

        delete set_params_data;
        return 0;
      },
      0, nullptr, 0, false, data);
}

} // namespace pw_utils
