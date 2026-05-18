#pragma once

#include <optional>
#include <string>
#include <variant>
#include <format>
#include <iostream>

#include "models/prop_info/range.h"
#include "enum.h"
#include "models/prop_info/string_values.h"
#include "models/prop_info/step_range.h"

namespace models::prop_info {
using BoolEnum = Enum<bool>;

using PropertyType = std::variant<
  IntegerRange, LongRange, FloatRange, DoubleRange, BoolEnum, StringValues,
  IntegerStepRange, LongStepRange, FloatStepRange, DoubleStepRange>;

class PropertyInfo {
public:
  std::string name;
  std::string description;
  std::optional<PropertyType> propertyType{};
  std::optional<std::string> container{};
  bool isParam = false;

  [[nodiscard]] std::expected<std::string, std::string> to_json() const {
    if (container && propertyType) {
      if (auto result = property_type_to_json(); result) {
        return std::format(R"({{
          "name": "{}",
          "description": "{}",
          "propertyType": {},
          "container": "{}",
          "isParam": {}
        }})", name, description, *result, container.value(),
                           isParam ? "true" : "false");
      } else {
        return result;
      }
    } else if (propertyType) {
      if (auto result = property_type_to_json(); result) {
        return std::format(R"({{
          "name": "{}",
          "description": "{}",
          "propertyType": {},
          "isParam": {}
        }})", name, description, *result, isParam ? "true" : "false");
      } else {
        return result;
      }
    } else if (container) {
      return std::format(R"({{
          "name": "{}",
          "description": "{}",
          "container": "{}",
          "isParam": {}
        }})", name, description, container.value(), isParam ? "true" : "false");
    } else {
      return std::format(R"({{
        "name": "{}",
        "description": "{}",
        "isParam": {}
      }})", name, description, isParam ? "true" : "false");
    }
  }

private:
  [[nodiscard]] std::expected<std::string, std::string>
  property_type_to_json() const;
};

[[nodiscard]] std::expected<std::string, std::string> property_infos_to_json(
  std::uint64_t object_serial, std::uint64_t object_id,
  const std::span<PropertyInfo> &property_infos);
}
