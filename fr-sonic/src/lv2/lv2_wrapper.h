#pragma once

#include "plugin.h"
#include "port.h"
#include "plugin_class.h"

#include <lilv/lilv.h>
#include <vector>

class Lv2 {
public:
  void init();
  void destroy();

  [[nodiscard]] std::vector<Plugin> get_all_plugins() const;
  [[nodiscard]] std::vector<PluginClass> get_all_plugin_classes() const;

private:
  LilvWorld *_lv2_world = nullptr;
  const LilvPlugins *_lv2_plugins = nullptr;

  static void get_all_ports_for_plugin(const LilvPlugin *plugin,
                                       std::vector<Port>& ports);
};
