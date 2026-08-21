#include "scene.hpp"

#include <algorithm>
#include <filesystem>
#include <fstream>
#include <iostream>

#include <nlohmann/json.hpp>

namespace scene {

namespace {

using nlohmann::json;

// Soft UI limit, enforced here so a scene never has more images than a
// SonicEddy object picker can offer slots for.
constexpr size_t kMaxImagesPerScene = 10;

bool parse_rotate(const json &object, uint32_t &out, const std::string &context) {
  if (!object.contains("rotate")) {
    out = 0;
    return true;
  }
  const auto value = object.at("rotate").get<int64_t>();
  if (value != 0 && value != 90 && value != 180 && value != 270) {
    std::cerr << context << ": \"rotate\" must be 0, 90, 180 or 270 (got "
              << value << ")\n";
    return false;
  }
  out = static_cast<uint32_t>(value);
  return true;
}

bool parse_object(const json &object, const std::filesystem::path &scene_dir,
                   size_t index, SceneObject &out) {
  const auto context = "objects[" + std::to_string(index) + "]";

  if (!object.contains("type") || !object.at("type").is_string()) {
    std::cerr << context << ": \"type\" is mandatory and must be a string\n";
    return false;
  }
  const auto type = object.at("type").get<std::string>();
  if (type == "camera") {
    out.type = ObjectType::Camera;
  } else if (type == "image") {
    out.type = ObjectType::Image;
  } else {
    std::cerr << context << ": unknown \"type\" \"" << type
               << "\" (expected \"camera\" or \"image\")\n";
    return false;
  }

  if (!object.contains("position") || !object.at("position").is_array() ||
      object.at("position").size() != 3) {
    std::cerr << context
               << ": \"position\" is mandatory and must be [x, y, z]\n";
    return false;
  }
  const auto &position = object.at("position");
  out.x = position.at(0).get<int32_t>();
  out.y = position.at(1).get<int32_t>();
  out.z = position.at(2).get<int32_t>();

  if (!object.contains("width") || !object.contains("height")) {
    std::cerr << context << ": \"width\" and \"height\" are mandatory\n";
    return false;
  }
  out.width = object.at("width").get<uint32_t>();
  out.height = object.at("height").get<uint32_t>();
  if (out.width == 0 || out.height == 0) {
    std::cerr << context << ": \"width\" and \"height\" must be > 0\n";
    return false;
  }

  out.flip_horizontal = object.value("flip_horizontal", false);
  out.flip_vertical = object.value("flip_vertical", false);
  if (!parse_rotate(object, out.rotate, context))
    return false;

  if (out.type == ObjectType::Camera) {
    if (!object.contains("target_camera_index")) {
      std::cerr << context
                 << ": \"target_camera_index\" is mandatory for camera objects\n";
      return false;
    }
    out.target_camera_index = object.at("target_camera_index").get<uint32_t>();
  } else {
    if (!object.contains("image_file") || !object.at("image_file").is_string()) {
      std::cerr << context
                 << ": \"image_file\" is mandatory for image objects\n";
      return false;
    }
    const auto image_file = object.at("image_file").get<std::string>();
    out.image_file = (scene_dir / image_file).string();
  }

  return true;
}

} // namespace

std::optional<SceneConfig> load_scene(const std::string &path) {
  std::ifstream in(path);
  if (!in) {
    std::cerr << "scene: failed to open \"" << path << "\"\n";
    return std::nullopt;
  }

  json root;
  try {
    in >> root;
  } catch (const json::exception &error) {
    std::cerr << "scene: failed to parse \"" << path << "\": " << error.what()
               << '\n';
    return std::nullopt;
  }

  if (!root.contains("canvas_width") || !root.contains("canvas_height")) {
    std::cerr << "scene: \"canvas_width\" and \"canvas_height\" are mandatory\n";
    return std::nullopt;
  }
  if (!root.contains("objects") || !root.at("objects").is_array()) {
    std::cerr << "scene: \"objects\" is mandatory and must be an array\n";
    return std::nullopt;
  }

  SceneConfig config;
  config.name = root.value("name", "");
  try {
    config.canvas_width = root.at("canvas_width").get<uint32_t>();
    config.canvas_height = root.at("canvas_height").get<uint32_t>();
  } catch (const json::exception &error) {
    std::cerr << "scene: invalid canvas size: " << error.what() << '\n';
    return std::nullopt;
  }

  const auto scene_dir = std::filesystem::path(path).parent_path();
  const auto &objects = root.at("objects");
  config.objects.reserve(objects.size());
  for (size_t i = 0; i < objects.size(); ++i) {
    SceneObject object;
    try {
      if (!parse_object(objects.at(i), scene_dir, i, object))
        return std::nullopt;
    } catch (const json::exception &error) {
      std::cerr << "scene: objects[" << i << "]: " << error.what() << '\n';
      return std::nullopt;
    }
    config.objects.push_back(std::move(object));
  }

  const auto image_count = std::count_if(
      config.objects.begin(), config.objects.end(),
      [](const SceneObject &object) { return object.type == ObjectType::Image; });
  if (static_cast<size_t>(image_count) > kMaxImagesPerScene) {
    std::cerr << "scene: " << image_count << " image objects exceeds the max of "
               << kMaxImagesPerScene << " per scene\n";
    return std::nullopt;
  }

  return config;
}

} // namespace scene
