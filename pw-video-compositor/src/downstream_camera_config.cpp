#include "downstream_camera_config.hpp"

#include <fstream>
#include <iostream>

#include <nlohmann/json.hpp>

namespace downstream_camera_config {

namespace {

using nlohmann::json;

bool parse_input(const json &entry, size_t index, InputDef &out) {
  const auto context = "inputs[" + std::to_string(index) + "]";

  if (!entry.contains("name") || !entry.at("name").is_string() ||
      entry.at("name").get<std::string>().empty()) {
    std::cerr << context << ": \"name\" is mandatory and must be a non-empty string\n";
    return false;
  }
  out.name = entry.at("name").get<std::string>();

  if (!entry.contains("width") || !entry.contains("height")) {
    std::cerr << context << ": \"width\" and \"height\" are mandatory\n";
    return false;
  }
  out.width = entry.at("width").get<uint32_t>();
  out.height = entry.at("height").get<uint32_t>();
  if (out.width == 0 || out.height == 0) {
    std::cerr << context << ": \"width\" and \"height\" must be > 0\n";
    return false;
  }

  return true;
}

} // namespace

std::optional<std::vector<InputDef>> load(const std::string &path) {
  std::ifstream in(path);
  if (!in) {
    std::cerr << "downstream_camera_config: failed to open \"" << path << "\"\n";
    return std::nullopt;
  }

  json root;
  try {
    in >> root;
  } catch (const json::exception &error) {
    std::cerr << "downstream_camera_config: failed to parse \"" << path
               << "\": " << error.what() << '\n';
    return std::nullopt;
  }

  if (!root.contains("inputs") || !root.at("inputs").is_array()) {
    std::cerr << "downstream_camera_config: \"inputs\" is mandatory and must be an array\n";
    return std::nullopt;
  }

  const auto &inputs = root.at("inputs");
  std::vector<InputDef> result;
  result.reserve(inputs.size());
  for (size_t i = 0; i < inputs.size(); ++i) {
    InputDef def;
    try {
      if (!parse_input(inputs.at(i), i, def))
        return std::nullopt;
    } catch (const json::exception &error) {
      std::cerr << "downstream_camera_config: inputs[" << i << "]: " << error.what() << '\n';
      return std::nullopt;
    }
    result.push_back(std::move(def));
  }

  return result;
}

} // namespace downstream_camera_config
