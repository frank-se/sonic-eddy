#include "lv2_wrapper.h"

#include <iostream>
#include <ostream>

void Lv2::init() {
  _lv2_world = lilv_world_new();
  lilv_world_set_option(_lv2_world, LILV_OPTION_OBJECT_INDEX, nullptr);
  lilv_world_load_all(_lv2_world);
  _lv2_plugins = lilv_world_get_all_plugins(_lv2_world);
}

void Lv2::destroy() {
  lilv_world_free(_lv2_world);
  _lv2_world = nullptr;
}

std::vector<Plugin> Lv2::get_all_plugins() const {
  std::vector<Plugin> result;
  LILV_FOREACH(plugins, i, _lv2_plugins) {
    const LilvPlugin *plugin = lilv_plugins_get(_lv2_plugins, i);
    const auto node_name = lilv_plugin_get_name(plugin);
    const auto node_uri = lilv_plugin_get_uri(plugin);
    const auto plugin_class = lilv_plugin_get_class(plugin);
    const auto plugin_class_node = lilv_plugin_class_get_uri(plugin_class);
    result.emplace_back(
        Plugin{.name = lilv_node_as_string(node_name),
               .uri = lilv_node_as_string(node_uri),
               .plugin_class_uri = lilv_node_as_string(plugin_class_node)});
    lilv_node_free(node_name);

    get_all_ports_for_plugin(plugin, result.back().port_descriptions);
  }
  return result;
}

void Lv2::get_all_ports_for_plugin(const LilvPlugin *plugin,
                                   std::vector<Port> &ports) {
  const uint32_t number_of_ports = lilv_plugin_get_num_ports(plugin);
  const auto minimums = new float[number_of_ports];
  const auto maximums = new float[number_of_ports];
  const auto defaults = new float[number_of_ports];
  lilv_plugin_get_port_ranges_float(plugin, minimums, maximums, defaults);

  for (uint32_t i = 0; i < number_of_ports; i++) {
    const auto port = lilv_plugin_get_port_by_index(plugin, i);
    const auto symbol_node = lilv_port_get_symbol(plugin, port);
    const auto name_node = lilv_port_get_name(plugin, port);

    ports.emplace_back(i, lilv_node_as_string(name_node),
                       lilv_node_as_string(symbol_node), minimums[i],
                       maximums[i], defaults[i]);
    auto &port_description = ports.back();

    if (!port) {
      std::cerr << "Could not find port " << i << std::endl;
      continue;
    }

    const auto class_nodes = lilv_port_get_classes(plugin, port);
    LILV_FOREACH(nodes, port_class_index, class_nodes) {
      const auto value = lilv_nodes_get(class_nodes, port_class_index);
      port_description.port_classes.emplace_back(lilv_node_as_string(value));
    }
  }

  delete[] minimums;
  delete[] maximums;
  delete[] defaults;
}

std::vector<PluginClass> Lv2::get_all_plugin_classes() const {
  std::vector<PluginClass> classes;
  const auto plugin_classes = lilv_world_get_plugin_classes(_lv2_world);
  LILV_FOREACH(plugin_classes, i, plugin_classes) {
    const auto plugin_class = lilv_plugin_classes_get(plugin_classes, i);
    const auto uri_node = lilv_plugin_class_get_uri(plugin_class);
    const auto label_node = lilv_plugin_class_get_label(plugin_class);
    const auto parent_uri_node = lilv_plugin_class_get_parent_uri(plugin_class);
    std::string parent_uri;
    if (parent_uri_node) {
      parent_uri = lilv_node_as_string(parent_uri_node);
    }
    classes.emplace_back(lilv_node_as_string(uri_node),
                         lilv_node_as_string(label_node), parent_uri);
  }
  return classes;
}