#pragma once

#include <functional>
#include <memory>
#include <thread>
#include <utility>

// ReSharper disable once CppUnusedIncludeDirective
#include <glib-unix.h>
#include <glib.h>
#include <pipewire/main-loop.h>

#include <wp/wp.h>

#include "metadata/metadata.h"
#include "metadata/metadata_collection.h"
#include "models/objects/wireplumber_object.h"
#include "models/props/props.h"

constexpr int ERR_COULD_NOT_CONNECT_WIREPLUMBER_CORE = 1;

using props_changed_callback_t = std::function<void(
    const models::props::Props *, const models::params::ParamUpdate *)>;

using props_enum_failed_callback_t = std::function<void(uint64_t)>;

using object_added_callback_t =
    std::function<void(const models::objects::wireplumber_object *)>;

using prop_info_added_callback_t = std::function<void(const char *)>;

using object_deleted_callback_t =
    std::function<void(uint64_t, models::objects::wireplumber_object_type)>;

class WireplumberThread {
public:
  explicit WireplumberThread(
      object_added_callback_t object_added_callback,
      props_changed_callback_t props_changed_callback,
      props_enum_failed_callback_t props_enumeration_failed_callback,
      prop_info_added_callback_t prop_info_added_callback,
      object_deleted_callback_t object_deleted_callback,
      metadata::metadata_added_callback_t metadata_added_callback,
      metadata::metadata_entry_updated_callback_t
          metadata_entry_updated_callback,
      metadata::metadata_entry_deleted_callback_t
          metadata_entry_deleted_callback)
      : _object_added_callback(std::move(object_added_callback)),
        _props_changed_callback(std::move(props_changed_callback)),
        _props_enumeration_failed_callback(
            std::move(props_enumeration_failed_callback)),
        _prop_info_added_callback(std::move(prop_info_added_callback)),
        _object_deleted_callback(std::move(object_deleted_callback)),
        _metadata_collection(*this, std::move(metadata_added_callback),
                             std::move(metadata_entry_updated_callback),
                             std::move(metadata_entry_deleted_callback)) {}

  int init();
  void start();
  void stop() const;

  void shutdown_loop();

  [[nodiscard]] WpPipewireObject *
  get_object_by_object_id(std::uint64_t object_id) const;

  [[nodiscard]] WpCore *wireplumber_core() const { return _wireplumber_core; }

  [[nodiscard]] GMainContext *wireplumber_context() const {
    return _main_context;
  }

  [[nodiscard]] pw_core *pipewire_core() const;

  [[nodiscard]] pw_context *pipewire_context() const;

  [[nodiscard]] pw_loop *pipewire_loop() const;

  void set_metadata_entry(const char *metadata_name, uint64_t subject,
                          const char *key, const char *type, const char *value);

  void delete_metadata_entry(const char *metadata_name, uint64_t subject,
                             const char *key);

  WpLink *create_link_by_port_id(uint64_t output_port_id,
                                 uint64_t input_port_id, bool linger) const;

  void delete_link(WpLink *link) const;

  void delete_link_object_id(uint64_t object_id) const;

private:
  object_added_callback_t _object_added_callback;
  props_changed_callback_t _props_changed_callback;
  props_enum_failed_callback_t _props_enumeration_failed_callback;
  prop_info_added_callback_t _prop_info_added_callback;
  object_deleted_callback_t _object_deleted_callback;

  std::shared_ptr<std::thread> _thread;
  GOptionContext *_options_context = nullptr;
  GMainLoop *_loop = nullptr;
  GMainContext *_main_context = nullptr;
  WpCore *_wireplumber_core = nullptr;
  WpObjectManager *_object_manager = nullptr;
  pw_core *_pipewire_core = nullptr;
  pw_context *_pipewire_context = nullptr;
  pw_loop *_pipewire_loop = nullptr;

  metadata::MetadataCollection _metadata_collection;

  static void g_signal_object_added_callback(WpObjectManager *object_manager,
                                             gpointer object,
                                             gpointer user_data);
  static void g_signal_object_removed_callback(WpObjectManager *object_manager,
                                               gpointer object,
                                               gpointer user_data);
  static void g_signal_params_changed_callback(WpPipewireObject *self,
                                               const gchar *key,
                                               gpointer user_data);

  static void wp_process_object_enum_prop_info_callback(GObject *source_object,
                                                        GAsyncResult *result,
                                                        gpointer user_data);

  static void wp_process_object_enum_props_callback(GObject *source_object,
                                                    GAsyncResult *result,
                                                    gpointer user_data);

  void trigger_object_added_callback(
      const models::objects::wireplumber_object &object) const;

  void trigger_props_changed_callback(
      const models::props::Props &props,
      const models::params::ParamUpdate &params) const;

  void trigger_props_enumeration_failed_callback(uint64_t object_serial) const;

  void trigger_object_deleted_callback(
      uint64_t object_serial,
      models::objects::wireplumber_object_type object_type) const;

  int setup_wireplumber();
  void setup_pipewire();
  void setup_object_manager();
  void register_signals();
  void run();
  void cleanup();
  static std::tuple<bool, WireplumberThread *, WpIterator *>
  finish_callback_and_prepare_iterator(GObject *source_object,
                                       GAsyncResult *res, gpointer user_data);
};
