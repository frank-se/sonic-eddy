#pragma once

#include "processing/WireplumberThread.h"

#include <spa/param/props.h>

namespace props::props_handling {
struct set_props_data {
  constexpr static size_t size = 4096;
  std::uint8_t buffer[size]{};
  WpSpaPod *pod = nullptr;
  WpPipewireObject *proxy = nullptr;
};

void set_volumes(uint64_t object_id, const double *volumes, uint64_t count,
                 const std::shared_ptr<WireplumberThread> &wireplumber_thread);

void set_mute(uint64_t object_id, bool mute,
              const std::shared_ptr<WireplumberThread> &wireplumber_thread);

[[nodiscard]] bool build_set_props_pod(
    const std::optional<std::tuple<const double *, size_t>> &volumesTuple,
    const std::optional<bool> &mute, set_props_data &data);

void invoke_set_param(
    set_props_data &props_data,
    const std::shared_ptr<WireplumberThread> &wireplumber_thread);
} // namespace props::props_handling