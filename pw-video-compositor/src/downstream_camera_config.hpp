#pragma once

#include <cstdint>
#include <optional>
#include <string>
#include <vector>

namespace downstream_camera_config {

// Mirrors pw-video-compositor's camera_config.hpp - one entry per routable
// overlay video-input slot (array order = slot index, the same index
// downstream_scene.json's "target_input_index" refers to). Unlike
// pw-video-compositor's --inputs, this list does NOT include the baseline
// input - see downstream_main.cpp's App::base_video_source, which is
// always present and never part of this pool.
struct InputDef {
  std::string name;
  uint32_t width = 0;
  uint32_t height = 0;
};

// Loads and validates an --inputs file. Same shape/error style as
// camera_config::load - "name" mandatory non-empty string, "width"/
// "height" mandatory and > 0. Unlike pw-video-compositor, an empty/absent
// --inputs is a normal, common case here (today's real Downstream usage
// has zero overlay video inputs) - callers should treat "not given at
// all" as an empty pool, not an error; this function is only invoked when
// a path was actually provided.
std::optional<std::vector<InputDef>> load(const std::string &path);

} // namespace downstream_camera_config
