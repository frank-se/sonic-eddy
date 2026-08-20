#pragma once

#include <cstdint>
#include <optional>
#include <string>
#include <vector>

namespace scene {

enum class ObjectType { Camera, Image };

struct SceneObject {
  ObjectType type = ObjectType::Camera;

  int32_t x = 0;
  int32_t y = 0;
  int32_t z = 0;

  uint32_t width = 0;
  uint32_t height = 0;

  bool flip_horizontal = false;
  bool flip_vertical = false;
  uint32_t rotate = 0; // 0, 90, 180 or 270

  // type == Camera
  uint32_t target_camera_index = 0;

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

// Loads and validates a scene file. `type` and `position` are mandatory on
// every object; transform fields default to identity when omitted; the
// remaining fields are validated per `type`. On any parse/validation
// failure, prints a diagnostic to stderr and returns nullopt - mirrors
// parse_args' error style in main.cpp, no exceptions cross this boundary.
std::optional<SceneConfig> load_scene(const std::string &path);

} // namespace scene
