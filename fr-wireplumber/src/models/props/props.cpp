#include "models/props/props.h"

#include <iostream>
#include <pipewire/keys.h>
#include <spa/debug/pod.h>
#include <spa/param/props.h>
#include <spa/pod/iter.h>
#include <wp/wp.h>

#include "spa_helpers/mapping_helper.h"
#include "models/params/param_update.h"

void
models::props::pipewire_props_fill_from_wp_spa_pod(WpPipewireObject *object,
  const WpSpaPod *param, models::props::Props &props) {
  const auto pod = wp_spa_pod_get_spa_pod(param);

  if (WP_IS_PORT(object)) {
    props.type = objects::wireplumber_object_type::port;
  } else if (WP_IS_LINK(object)) {
    props.type = objects::wireplumber_object_type::link;
  } else if (WP_IS_NODE(object)) {
    props.type = objects::wireplumber_object_type::node;
  } else if (WP_IS_DEVICE(object)) {
    props.type = objects::wireplumber_object_type::device;
  } else if (WP_IS_CLIENT(object)) {
    props.type = objects::wireplumber_object_type::client;
  }

  fill_uint64(props.object_id, object, PW_KEY_OBJECT_ID);
  fill_uint64(props.object_serial, object, PW_KEY_OBJECT_SERIAL);

  const auto volume = spa_pod_find_prop(pod, nullptr, SPA_PROP_volume);
  if (volume != nullptr) {
    spa_pod_get_float(&volume->value, &props.volume);
  }

  const auto mute = spa_pod_find_prop(pod, nullptr, SPA_PROP_mute);
  if (mute != nullptr) {
    spa_pod_get_bool(&mute->value, &props.mute);
  }

  const auto channel_volumes = spa_pod_find_prop(
    pod, nullptr, SPA_PROP_channelVolumes);
  if (channel_volumes != nullptr) {
    if (spa_pod_is_array(&channel_volumes->value)) {
      props.channel_volumes_size = SPA_POD_ARRAY_N_VALUES(
        &channel_volumes->value);
      const auto elements = SPA_POD_ARRAY_VALUES(&channel_volumes->value);
      const auto element_type = SPA_POD_ARRAY_VALUE_TYPE(
        &channel_volumes->value);

      if (element_type == SPA_TYPE_Float) {
        const auto float_values = static_cast<float*>(elements);
        props.channel_volumes = new float[props.channel_volumes_size];
        std::copy_n(float_values, props.channel_volumes_size,
                    props.channel_volumes);
      }
    }
  }

  const auto soft_mute = spa_pod_find_prop(pod, nullptr, SPA_PROP_softMute);
  if (soft_mute != nullptr) {
    spa_pod_get_bool(&soft_mute->value, &props.soft_mute);
  }

  const auto soft_volumes = spa_pod_find_prop(pod, nullptr,
                                              SPA_PROP_softVolumes);
  if (soft_volumes != nullptr) {
    props.soft_volumes_size = SPA_POD_ARRAY_N_VALUES(&soft_volumes->value);
    const auto elements = SPA_POD_ARRAY_VALUES(&soft_volumes->value);
    const auto element_type = SPA_POD_ARRAY_VALUE_TYPE(&soft_volumes->value);

    if (element_type == SPA_TYPE_Float) {
      const auto float_values = static_cast<float*>(elements);
      props.soft_volumes = new float[props.soft_volumes_size];
      std::copy_n(float_values, props.soft_volumes_size, props.soft_volumes);
    }
  }

  const auto monitor_mute = spa_pod_find_prop(pod, nullptr,
                                              SPA_PROP_monitorMute);
  if (monitor_mute != nullptr) {
    spa_pod_get_bool(&monitor_mute->value, &props.monitor_mute);
  }

  const auto monitor_volumes = spa_pod_find_prop(
    pod, nullptr, SPA_PROP_monitorVolumes);
  if (monitor_volumes != nullptr) {
    props.monitor_volumes_size =
      SPA_POD_ARRAY_N_VALUES(&monitor_volumes->value);
    const auto elements = SPA_POD_ARRAY_VALUES(&monitor_volumes->value);
    const auto element_type = SPA_POD_ARRAY_VALUE_TYPE(&monitor_volumes->value);

    if (element_type == SPA_TYPE_Float) {
      const auto float_values = static_cast<float*>(elements);
      props.monitor_volumes = new float[props.monitor_volumes_size];
      std::copy_n(float_values, props.monitor_volumes_size,
                  props.monitor_volumes);
    }
  }

  const auto channel_map = spa_pod_find_prop(pod, nullptr, SPA_PROP_channelMap);
  if (channel_map != nullptr) {
    props.channel_map_size = SPA_POD_ARRAY_N_VALUES(&channel_map->value);
    const auto elements = SPA_POD_ARRAY_VALUES(&channel_map->value);
    const auto element_type = SPA_POD_ARRAY_VALUE_TYPE(&channel_map->value);

    if (element_type == SPA_TYPE_Id) {
      const auto id_values = static_cast<uint32_t*>(elements);
      props.channel_map = new uint32_t[props.channel_map_size];
      std::copy_n(id_values, props.channel_map_size, props.channel_map);
    }
  }
}

void models::props::pipewire_props_delete(Props &props) {
  delete[] props.channel_map;
  props.channel_map = nullptr;

  delete[] props.channel_volumes;
  props.channel_volumes = nullptr;

  delete[] props.soft_volumes;
  props.soft_volumes = nullptr;

  delete[] props.monitor_volumes;
  props.monitor_volumes = nullptr;
}
