#include "frwireplumber.h"

#include <iostream>
#include <memory>
#include <ostream>
#include <pipewire/impl-module.h>
#include <pipewire/pipewire.h>
#include <spa/param/param.h>
#include <spa/param/props.h>
#include <spa/pod/builder.h>

#include "modules/module_factory.h"
#include "params/params_handling.h"
#include "processing/WireplumberThread.h"
#include "props/props_handling.h"

std::shared_ptr<WireplumberThread> g_thread = nullptr;

void init(
    node_added_callback_pointer_t node_added_callback,
    props_changed_callback_pointer_t props_changed_callback,
    props_enum_failed_callback_pointer_t props_enum_failed_callback,
    prop_info_added_callback_pointer_t prop_info_added_callback,
    object_deleted_callback_pointer_t object_deleted_callback,
    metadata_added_callback_pointer_t metadata_added_callback,
    metadata_entry_updated_callback_pointer_t metadata_entry_updated_callback,
    metadata_entry_deleted_callback_pointer_t metadata_entry_deleted_callback) {
  if (node_added_callback == nullptr) {
    std::cerr << "ERROR: Node added callback is null" << std::endl;
  }

  if (props_changed_callback == nullptr) {
    std::cerr << "ERROR: Props changed callback is null" << std::endl;
  }

  g_thread = std::make_shared<WireplumberThread>(
      node_added_callback, props_changed_callback, props_enum_failed_callback,
      prop_info_added_callback, object_deleted_callback,
      metadata_added_callback, metadata_entry_updated_callback,
      metadata_entry_deleted_callback);
}

void start() { g_thread->start(); }

void stop() { g_thread->stop(); }

void set_volumes(const std::uint64_t object_id, const double *volume,
                 const std::size_t count) {
  props::props_handling::set_volumes(object_id, volume, count, g_thread);
}

void set_mute(uint64_t object_id, bool mute) {
  props::props_handling::set_mute(object_id, mute, g_thread);
}

void set_params(const uint64_t object_id, char **keys,
                models::params::ParamType *param_types, uint64_t *values,
                const size_t count) {
  params::params_handling::set_params(object_id, keys, param_types, values,
                                      count, g_thread);
}

bool load_module(const char *module_name, const char *module_config,
                 void **module_handle) {
  return modules::modules_factory::load_module(module_name, module_config,
                                               module_handle, g_thread);
}

void destroy_module(void *module_handle) {
  modules::modules_factory::destroy_module(module_handle);
}
void set_metadata_entry(const char *metadata_name, uint64_t subject,
                        const char *key, const char *type, const char *value) {
  g_thread->set_metadata_entry(metadata_name, subject, key, type, value);
}

void delete_metadata_entry(const char *metadata_name, uint64_t subject,
                           const char *key) {
  g_thread->delete_metadata_entry(metadata_name, subject, key);
}

WpLink *create_link_by_port_ids(const uint64_t output_port_id,
                                const uint64_t input_port_id,
                                const bool linger) {
  return g_thread->create_link_by_port_id(output_port_id, input_port_id,
                                          linger);
}

void delete_link_by_object_id(const uint64_t object_id) {
  g_thread->delete_link_object_id(object_id);
}

void delete_link(WpLink *link) { g_thread->delete_link(link); }
