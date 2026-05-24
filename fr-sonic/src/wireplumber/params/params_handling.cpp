#include "params/params_handling.h"

#include "core/Core.h"

#include <iostream>
#include <ostream>
#include <spa/debug/pod.h>
#include <spa/param/param.h>
#include <spa/param/props.h>
#include <spa/pod/builder.h>

void params::params_handling::build_set_params_pod(
    char **keys, const models::params::ParamType *param_types, uint64_t *values,
    const size_t count, set_params_data &data) {
  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, data.buffer,
                       params::params_handling::set_params_data::size);

  spa_pod_frame object_frame{};
  spa_pod_builder_push_object(&builder, &object_frame, SPA_TYPE_OBJECT_Props,
                              SPA_PARAM_Props);
  spa_pod_builder_prop(&builder, SPA_PROP_params, 0);

  spa_pod_frame struct_frame{};
  spa_pod_builder_push_struct(&builder, &struct_frame);
  for (size_t i = 0; i < count; i++) {
    spa_pod_builder_string(&builder, keys[i]);
    switch (param_types[i]) {
    case models::params::ParamType::Bool: {
      const auto value = static_cast<uint32_t>(values[i]) != 0;
      spa_pod_builder_bool(&builder, value);
      break;
    }
    case models::params::ParamType::Int: {
      const auto value = static_cast<int32_t>(values[i]);
      spa_pod_builder_int(&builder, value);
      break;
    }
    case models::params::ParamType::Long: {
      const auto value = static_cast<int64_t>(values[i]);
      spa_pod_builder_long(&builder, value);
      break;
    }
    case models::params::ParamType::Float: {
      auto bits = static_cast<uint32_t>(values[i]);
      const float value = *reinterpret_cast<float *>(&bits);
      spa_pod_builder_float(&builder, value);
      break;
    }
    case models::params::ParamType::Double: {
      const auto value = *reinterpret_cast<double *>(values[i]);
      spa_pod_builder_double(&builder, value);
      break;
    }
    case models::params::ParamType::String: {
      const auto value = reinterpret_cast<char *>(values[i]);
      spa_pod_builder_string(&builder, value);
      break;
    }
    default:
      break;
    }
  }

  spa_pod_builder_pop(&builder, &struct_frame);
  auto pod =
      static_cast<spa_pod *>(spa_pod_builder_pop(&builder, &object_frame));

  spa_debug_pod(0, nullptr, pod);

  data.pod = wp_spa_pod_new_wrap(pod);
}

void params::params_handling::set_params(
    uint64_t object_id, char **keys, models::params::ParamType *param_types,
    uint64_t *values, size_t count,
    const std::shared_ptr<Core> &wireplumber_thread) {
  const auto proxy = wireplumber_thread->get_object_by_object_id(object_id);

  if (proxy == nullptr) {
    std::cerr << "proxy is nullptr" << std::endl;
    return;
  }

  if (count == 0)
    return;

  auto data = new set_params_data();
  data->proxy = proxy;

  build_set_params_pod(keys, param_types, values, count, *data);

  g_object_ref(data->proxy);

  g_main_context_invoke(
      wireplumber_thread->wireplumber_context(),
      [](gpointer data) {
        const auto inner_data = static_cast<set_params_data *>(data);
        wp_pipewire_object_set_param(inner_data->proxy, "Props", 0,
                                     inner_data->pod);
        g_object_unref(inner_data->proxy);
        delete inner_data;
        return static_cast<gboolean>(false);
      },
      data);
}
