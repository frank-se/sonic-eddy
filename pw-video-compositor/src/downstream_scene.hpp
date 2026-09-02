#pragma once

#include <cstdint>
#include <optional>
#include <string>
#include <vector>

namespace downstream_scene {

// Video (not Camera - "video in can include ANYTHING", not just a camera)
// references target_input_index into the --inputs-loaded pool
// (downstream_camera_config). Image is unchanged - static, decoded at
// startup. Neither type can address the baseline input - see
// downstream_main.cpp's App::base_video_source, which is implicit and
// never a scene object at all.
enum class ObjectType { Video, Image };

struct SceneObject {
  ObjectType type = ObjectType::Video;

  int32_t x = 0;
  int32_t y = 0;
  int32_t z = 0;

  uint32_t width = 0;
  uint32_t height = 0;

  bool flip_horizontal = false;
  bool flip_vertical = false;
  uint32_t rotate = 0; // 0, 90, 180 or 270

  // type == Video
  uint32_t target_input_index = 0;

  // type == Image - absolute path, already resolved relative to the scene
  // file's own directory.
  std::string image_file;
};

struct SceneConfig {
  std::string name;
  uint32_t canvas_width = 0;
  uint32_t canvas_height = 0;
  std::vector<SceneObject> objects;
};

// Loads and validates a scene file. Same shape/error style as
// scene::load_scene - "type" and "position" mandatory on every object,
// transform fields default to identity, prints a diagnostic to stderr and
// returns nullopt on any failure, no exceptions cross this boundary.
std::optional<SceneConfig> load_scene(const std::string &path);

} // namespace downstream_scene
