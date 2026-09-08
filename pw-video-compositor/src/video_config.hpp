#pragma once

#include <cstdint>
#include <optional>
#include <string>
#include <vector>

namespace video_config {

// One entry per shared input slot (array order = slot index, the same index
// scene.json's "target_input_index" refers to). Stable across scenes -
// loaded once per compositor process, independent of which/how many
// --scene files are also loaded, so a slot always exists for routing
// regardless of what any given scene currently references. Not
// camera-specific - target_object can point at any PipeWire video
// producer (a physical camera, a screen capture, a generated stream like
// se.midi-cube.out, se.mixer-overview, ...).
struct InputDef {
  std::string name;
  uint32_t width = 0;
  uint32_t height = 0;
  // Optional PW_KEY_TARGET_OBJECT (node name or object.serial) to auto-link
  // this input to on startup - empty means no auto-link (route it later
  // with a manual pw-link, today's default).
  std::string target_object;
};

// Loads and validates a video-input-definition file. Every entry's "name"
// must be a non-empty string, "width"/"height" mandatory and > 0. On any
// parse/validation failure, prints a diagnostic to stderr and returns
// nullopt - mirrors scene::load_scene's error style, no exceptions cross
// this boundary.
std::optional<std::vector<InputDef>> load(const std::string &path);

} // namespace video_config
