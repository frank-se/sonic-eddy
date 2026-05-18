#pragma once

#include <cstdint>
#include <format>
#include <math.h>
#include <string>
#include <vector>

struct Port {
  uint32_t index;
  std::string name;
  std::string symbol;
  float min;
  float max;
  float default_;
  std::vector<std::string> port_classes;

  std::string to_json() const {
    auto port_classes_string = port_classes_array();
    if (std::isnan(min) || std::isnan(max) || std::isnan(default_))
      return std::format(R"({{
      "index": {},
      "name": "{}",
      "symbol": "{}",
      "classes": {}
    }})",
                         index, name, symbol, port_classes_string);

    return std::format(R"({{
      "index": {},
      "name": "{}",
      "symbol": "{}",
      "minimum": {},
      "maximum": {},
      "default": {},
      "classes": {}
    }})",
                       index, name, symbol, min, max, default_,
                       port_classes_string);
  }

  std::string port_classes_array() const {
    std::string json = "[";
    for (auto &&port_class : port_classes) {
      json += '"';
      json += port_class;
      json += "\",";
    }
    json.back() = ']';
    return json;
  }
};