#pragma once

#include <cstdint>
#include <optional>
#include <tuple>

#include "../objects/wireplumber_object.h"
#include "models/params/param_update.h"

namespace models::props {
struct object_identity {
  std::uint64_t object_id = 0;
  std::uint64_t object_serial = 0;
};

struct Props {
  objects::wireplumber_object_type type =
      objects::wireplumber_object_type::node;

  std::uint64_t object_id = 0;
  std::uint64_t object_serial = 0;

  float volume = 0.0f;
  bool mute = false;

  float *channel_volumes = nullptr;
  std::uint64_t channel_volumes_size = 0;

  bool soft_mute = false;

  float *soft_volumes = nullptr;
  std::uint64_t soft_volumes_size = 0;

  bool monitor_mute = false;

  float *monitor_volumes = nullptr;
  std::uint64_t monitor_volumes_size = 0;

  /*
   * The enum to interpret the channel map values can be found here:
   *
   * #include <spa/param/audio/raw.h>
   */
  std::uint32_t *channel_map = nullptr;
  std::uint64_t channel_map_size = 0;
};

void pipewire_props_fill_from_wp_spa_pod(WpPipewireObject *object,
                                         const WpSpaPod *param, Props &props);

void pipewire_props_delete(Props &props);
} // namespace models::props
