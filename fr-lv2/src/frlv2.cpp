#include "frlv2.h"

#include "lv2_wrapper.h"

Lv2 lv2;
std::string plugin_descriptions_json_data{};
std::string plugin_classes_json_data{};

void init() { lv2.init(); }

void destroy() { lv2.destroy(); }

const char *plugin_descriptions_json() {
  if (plugin_descriptions_json_data.empty()) {
    plugin_descriptions_json_data = "[";
    const auto plugins = lv2.get_all_plugins();
    for (const auto &plugin : plugins) {
      plugin_descriptions_json_data += plugin.to_json();
      plugin_descriptions_json_data += ",";
    }
    plugin_descriptions_json_data.back() = ']';
  }

  return plugin_descriptions_json_data.c_str();
}

const char *plugin_classes_json() {
  if (plugin_classes_json_data.empty()) {
    plugin_classes_json_data = "[";
    const auto plugin_classes = lv2.get_all_plugin_classes();
    for (const auto &plugin_class : plugin_classes) {
      plugin_classes_json_data += plugin_class.to_json();
      plugin_classes_json_data += ",";
    }
    plugin_classes_json_data.back() = ']';
  }
  return plugin_classes_json_data.c_str();
}