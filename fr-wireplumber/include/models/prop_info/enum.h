#pragma once

#include <vector>

namespace models::prop_info {
template <is_any_of<bool> T>
class Enum {
public:
  Enum(T default_, std::vector<T> values)
    : default_(std::move(default_)), values(std::move(values)) {}

  Enum() = default;

  T default_;
  std::vector<T> values{};

  [[nodiscard]] std::expected<std::string, std::string> to_json() const {
    auto values_json_array = values_to_json();
    return std::format(R"(
    {{
      "type": "BoolEnum",
      "default": {},
      "values": {}
    }})", default_, values_json_array);
  }

private:
  [[nodiscard]] std::string values_to_json() const {
    if (values.empty()) return "[]";

    std::string values_json_array;
    auto expected_size = 2 + 7 * values.size();
    values_json_array.reserve(expected_size);
    values_json_array += "[";

    for (auto value : values) {
      values_json_array += " ";
      values_json_array += value ? "true" : "false";
      values_json_array += ",";
    }

    values_json_array.back() = ' ';
    values_json_array += "]";

    return values_json_array;
  }
};
}
