#include "models/prop_info/prop_info.h"

#include <iostream>
#include <ostream>

std::expected<std::string, std::string> models::prop_info::PropertyInfo::
property_type_to_json() const {
  std::string property_type_json;
  const auto property_type = propertyType.value();
  return std::visit([](const auto& p) {
    return p.to_json();
  }, property_type);
}

std::expected<std::string, std::string>
models::prop_info::property_infos_to_json(std::uint64_t object_serial,
                                          std::uint64_t object_id,
                                          const std::span<PropertyInfo> &
                                          property_infos) {
  std::string result = std::format(
    (R"({{
        "objectSerial": {},
        "objectId": {},
        "propertyInfos": [ )"),
    object_serial, object_id);

  for (const auto &property_info : property_infos) {
    if (const auto property_info_json = property_info.to_json();
      property_info_json) {
      result += " ";
      result += *property_info_json;
      result += ",";
    } else {
      std::cerr << property_info_json.error() << std::endl;
    }
  }

  result.back() = ' ';
  result += "]}";

  return result;
}
