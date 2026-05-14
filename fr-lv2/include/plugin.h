#pragma once
#include "port.h"

#include <format>
#include <string>
#include <vector>

struct Plugin {
  std::string name;
  std::string uri;
  std::string plugin_class_uri;
  std::vector<Port> port_descriptions{};

  [[nodiscard]] std::string to_json() const {
    auto port_description_array_json = port_description_array();
    return std::format(R"({{
      "name": "{}",
      "uri": "{}",
      "pluginClassUri": "{}",
      "ports": {}
    }})", name, uri, plugin_class_uri, port_description_array_json);
  };

  [[nodiscard]] std::string port_description_array() const {
    std::string array = "[ ";
    for (auto &&port_description : port_descriptions) {
      array += port_description.to_json();
      array += ",";
    }
    array.back() = ']';
    return array;
  }
};
