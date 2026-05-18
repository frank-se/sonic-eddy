#pragma once
#include <string>
#include <vector>

namespace models::prop_info {
class StringValues {
public:
  StringValues(std::string value, std::vector<std::string> labels)
    : value(std::move(value)), labels(std::move(labels)) {}

  StringValues() = default;

  std::string value;
  std::vector<std::string> labels;

  [[nodiscard]] std::expected<std::string, std::string> to_json() const {
    auto labels_json_array = labels_to_json();
    return std::format(R"(
    {{
      "type": "StringValues",
      "value": "{}",
      "labels": {}
    }})", value, labels_json_array);
  }

private:
  [[nodiscard]] std::string labels_to_json() const {
    if (labels.empty()) return "[]";

    std::string labels_json_array;

    std::size_t combined_string_sizes(0);
    for (const auto &label : labels) {
      combined_string_sizes += label.size();
    }
    auto expected_size = 2 + combined_string_sizes * labels.size();
    labels_json_array.reserve(expected_size);
    labels_json_array += "[";

    for (const auto &label : labels) {
      labels_json_array += " \"";
      labels_json_array += label;
      labels_json_array += "\",";
    }

    labels_json_array.back() = ' ';
    labels_json_array += "]";

    return labels_json_array;
  }
};
}
