#include "props/props_handling.h"

#include <iostream>
#include <spa/debug/pod.h>
#include <spa/param/param.h>
#include <spa/param/props.h>
#include <spa/pod/builder.h>

void props::props_handling::set_volumes(
    const uint64_t object_id, const double *volumes, const uint64_t count,
    const std::shared_ptr<WireplumberThread> &wireplumber_thread) {
  if (volumes == nullptr) {
    std::cerr << "Volume pointer is null" << std::endl;
    return;
  }

  if (count == 0)
    return;

  const auto proxy = wireplumber_thread->get_object_by_object_id(object_id);

  if (proxy == nullptr)
    return;

  const auto props_data = new set_props_data{.proxy = proxy};

  if (!build_set_props_pod(std::make_tuple(volumes, count), std::nullopt,
                           *props_data)) {
    delete props_data;
    return;
  }

  invoke_set_param(*props_data, wireplumber_thread);
}

void props::props_handling::set_mute(
    const uint64_t object_id, const bool mute,
    const std::shared_ptr<WireplumberThread> &wireplumber_thread) {
  const auto proxy = wireplumber_thread->get_object_by_object_id(object_id);

  if (proxy == nullptr) {
    std::cerr << "Couldn't get proxy " << object_id << std::endl;
    return;
  }

  const auto props_data = new set_props_data{.proxy = proxy};

  if (!build_set_props_pod(std::nullopt, mute, *props_data)) {
    std::cerr << "Couldn't build props pod" << std::endl;
    delete props_data;
    return;
  }

  invoke_set_param(*props_data, wireplumber_thread);
}

bool props::props_handling::build_set_props_pod(
    const std::optional<std::tuple<const double *, size_t>> &volumesTuple,
    const std::optional<bool> &mute, set_props_data &props_data) {
  if (!volumesTuple && !mute)
    return false;

  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, &props_data.buffer, sizeof(props_data.buffer));

  spa_pod_frame frame{};
  spa_pod_builder_push_object(&builder, &frame, SPA_TYPE_OBJECT_Props,
                              SPA_PARAM_Props);

  if (volumesTuple) {
    auto [volumes, count] = *volumesTuple;
    spa_pod_builder_prop(&builder, SPA_PROP_channelVolumes, 0);

    spa_pod_frame array_frame{};
    spa_pod_builder_push_array(&builder, &array_frame);

    for (std::size_t i = 0; i < count; i++) {
      spa_pod_builder_float(&builder, static_cast<float>(volumes[i]));
    }

    spa_pod_builder_pop(&builder, &array_frame);
  }

  if (mute) {
    spa_pod_builder_prop(&builder, SPA_PROP_mute, 0);
    spa_pod_builder_bool(&builder, *mute);
  }

  const auto pod =
      static_cast<struct spa_pod *>(spa_pod_builder_pop(&builder, &frame));

  props_data.pod = wp_spa_pod_new_wrap(pod);
  return true;
}

void props::props_handling::invoke_set_param(
    set_props_data &props_data,
    const std::shared_ptr<WireplumberThread> &wireplumber_thread) {
  g_object_ref(props_data.proxy);

  g_main_context_invoke(
      wireplumber_thread->wireplumber_context(),
      [](gpointer data) {
        const auto inner_props_data = static_cast<set_props_data *>(data);
        wp_pipewire_object_set_param(inner_props_data->proxy, "Props", 0,
                                     inner_props_data->pod);
        g_object_unref(inner_props_data->proxy);
        delete inner_props_data;
        return static_cast<gboolean>(false);
      },
      &props_data);
}
