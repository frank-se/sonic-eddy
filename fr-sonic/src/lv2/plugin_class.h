#pragma once

#include <string>

struct PluginClass {
  std::string uri;
  std::string label;
  std::string parent_uri;

  [[nodiscard]] std::string to_json() const {
    if (parent_uri.empty()) {
      return std::format(R"({{
        "uri": "{}",
        "label": "{}"
      }})", uri, label);
    }

    return std::format(R"({{
      "uri": "{}",
      "label": "{}",
      "parentUri": "{}"
    }})", uri, label, parent_uri);
  }
};
