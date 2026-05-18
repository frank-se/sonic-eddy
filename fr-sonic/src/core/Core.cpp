#include <cmath>
#include <csignal>
#include <format>
#include <glib.h>
#include <iostream>
#include <memory>
#include <ostream>
#include <pipewire/pipewire.h>
#include <spa/debug/pod.h>
#include <spa/pod/iter.h>
#include <spa/pod/parser.h>
#include <thread>
#include <wp/wp.h>

#include "models/prop_info/prop_info.h"
#include "models/props/props.h"
#include "Core.h"

#include "spa_helpers/mapping_helper.h"
#include "spa_helpers/spa_pod_helpers.h"

std::tuple<bool, Core *, WpIterator *>
Core::finish_callback_and_prepare_iterator(GObject *source_object,
                                                        GAsyncResult *res,
                                                        gpointer user_data) {
  const auto this_ = static_cast<Core *>(user_data);
  g_autoptr(WpIterator) it = nullptr;
  g_autoptr(GError) error = nullptr;

  it = wp_pipewire_object_enum_params_finish(WP_PIPEWIRE_OBJECT(source_object),
                                             res, &error);

  if (error) {
    return std::make_tuple(false, nullptr, nullptr);
  }

  if (!it)
    std::make_tuple(false, nullptr, nullptr);

  return std::make_tuple(true, this_, g_steal_pointer(&it));
}

static inline const char *get_prop_key_name(uint32_t key) {
  if (key >= 0x01000000) {
    return spa_debug_type_find_short_name(spa_type_param, key);
  } else if (key < 0x100) {
    return spa_debug_type_find_short_name(spa_type_prop_info, key);
  } else {
    return spa_debug_type_find_short_name(spa_type_props, key);
  }
}

void Core::wp_process_object_enum_props_callback(
    GObject *source_object, GAsyncResult *result, gpointer user_data) {
  auto [success, this_, iterator_pointer] =
      finish_callback_and_prepare_iterator(source_object, result, user_data);

  const auto self = WP_PIPEWIRE_OBJECT(source_object);
  if (success == false) {
    uint64_t object_serial;
    fill_uint64(object_serial, self, PW_KEY_OBJECT_SERIAL);
    this_ = static_cast<Core *>(user_data);
    this_->trigger_props_enumeration_failed_callback(object_serial);
  }

  if (success && iterator_pointer != nullptr) {
    g_autoptr(WpIterator) iterator = iterator_pointer;

    GValue item = G_VALUE_INIT;
    g_value_init(&item, WP_TYPE_SPA_POD);

    models::props::Props props{};
    models::params::ParamUpdate params_update{};
    while (wp_iterator_next(iterator, &item)) {
      const auto param_pod = static_cast<WpSpaPod *>(g_value_get_boxed(&item));
      const auto pod_type = wp_spa_pod_get_spa_type(param_pod);

      if (pod_type == SPA_TYPE_OBJECT_Props) {
        models::props::pipewire_props_fill_from_wp_spa_pod(self, param_pod,
                                                           props);

        const auto pod = wp_spa_pod_get_spa_pod(param_pod);
        const auto params_pod =
            spa_pod_find_prop(pod, nullptr, SPA_PROP_params);
        if (params_pod != nullptr) {
          models::params::fill_params_from_pod(&params_pod->value,
                                               params_update);
        }
      }

      g_value_unset(&item);
      g_value_init(&item, WP_TYPE_SPA_POD);
    }

    this_->trigger_props_changed_callback(props, params_update);

    models::props::pipewire_props_delete(props);
    models::params::params_data_delete(params_update);
  }
}

void Core::wp_process_object_enum_prop_info_callback(
    GObject *source_object, GAsyncResult *result, gpointer user_data) {
  auto [success, this_, iterator_pointer] =
      finish_callback_and_prepare_iterator(source_object, result, user_data);

  if (success && iterator_pointer != nullptr) {
    g_autoptr(WpIterator) iterator = iterator_pointer;

    GValue item = G_VALUE_INIT;
    g_value_init(&item, WP_TYPE_SPA_POD);

    std::vector<models::prop_info::PropertyInfo> property_infos;
    while (wp_iterator_next(iterator, &item)) {
      const auto param = static_cast<WpSpaPod *>(g_value_get_boxed(&item));
      const auto pod = wp_spa_pod_get_spa_pod(param);
      const struct spa_pod *prop_info_pod;
      SPA_POD_FOREACH(pod, SPA_POD_SIZE(pod), prop_info_pod) {
        auto &prop_info = property_infos.emplace_back();
        const auto prop_id =
            spa_pod_find_prop(prop_info_pod, nullptr, SPA_PROP_INFO_id);
        if (prop_id != nullptr) {
          std::uint32_t current_prop_id;
          spa_pod_get_id(&prop_id->value, &current_prop_id);
          if (get_prop_key_name(current_prop_id) == nullptr) {
            // Ignore broken parameters
            continue;
          }

          prop_info.name = get_prop_key_name(current_prop_id);
        } else {
          const auto prop_name =
              spa_pod_find_prop(prop_info_pod, nullptr, SPA_PROP_INFO_name);
          if (prop_name != nullptr) {
            const char *name;
            spa_pod_get_string(&prop_name->value, &name);
            prop_info.name = name;
          }
        }

        const auto prop_description = spa_pod_find_prop(
            prop_info_pod, nullptr, SPA_PROP_INFO_description);

        if (prop_description != nullptr) {
          const char *description;
          spa_pod_get_string(&prop_description->value, &description);
          prop_info.description = description;
        }

        const auto prop_type =
            spa_pod_find_prop(prop_info_pod, nullptr, SPA_PROP_INFO_type);
        if (prop_type != nullptr) {
          if (prop_type->value.type == SPA_TYPE_String) {
            const char *string_value;
            spa_pod_get_string(&prop_type->value, &string_value);
            auto labels_pod =
                spa_pod_find_prop(prop_info_pod, nullptr, SPA_PROP_INFO_labels);
            std::vector<std::string> labels;
            if (labels_pod != nullptr &&
                labels_pod->value.type == SPA_TYPE_Struct) {
              spa_pod *child = nullptr;
              auto pod_body_pointer =
                  spa_helpers::get_pod_body(&labels_pod->value);
              SPA_POD_FOREACH(pod_body_pointer,
                              SPA_POD_BODY_SIZE(&labels_pod->value), child) {
                if (child->type == SPA_TYPE_String) {
                  const char *struct_value;
                  spa_pod_get_string(child, &struct_value);
                  labels.emplace_back(struct_value);
                } else {
                  std::cerr << "Unsupported type in struct" << std::endl;
                }
              }
            }
            prop_info.propertyType = models::prop_info::StringValues(
                std::string(string_value), labels);
          } else if (prop_type->value.type == SPA_TYPE_Choice) {
            uint32_t number_of_values, choice_type;
            auto values = spa_pod_get_values(&prop_type->value,
                                             &number_of_values, &choice_type);
            if (choice_type == SPA_CHOICE_Range) {
              if (auto result =
                      models::prop_info::range_from_spa_choice_values(values);
                  result) {
                std::visit(
                    [&prop_info](const auto &x) {
                      using T = std::decay_t<decltype(x)>;
                      prop_info.propertyType.emplace(std::in_place_type<T>, x);
                    },
                    *result);
              } else {
                std::cerr << "Error: " << result.error() << std::endl;
                spa_debug_pod(4, nullptr, prop_info_pod);
                continue;
              }
            } else if (choice_type == SPA_CHOICE_Enum) {
              if (values->type == SPA_TYPE_Bool) {
                const auto values_array =
                    static_cast<uint32_t *>(SPA_POD_BODY(values));
                prop_info.propertyType = models::prop_info::BoolEnum(
                    values_array[0] ? true : false,
                    {values_array[1] ? true : false,
                     values_array[2] ? true : false});
              } else {
                std::cerr << "Error: unknown enum type" << std::endl;
                spa_debug_pod(4, nullptr, prop_info_pod);
              }
            } else if (choice_type == SPA_CHOICE_Step) {
              if (number_of_values == 4) {
                if (auto result =
                        models::prop_info::step_range_from_spa_choice_values(
                            values);
                    result) {
                  std::visit(
                      [&prop_info](const auto &x) {
                        using T = std::decay_t<decltype(x)>;
                        prop_info.propertyType.emplace(std::in_place_type<T>,
                                                       x);
                      },
                      *result);
                } else {
                  std::cerr << "Error: " << result.error() << std::endl;
                  spa_debug_pod(4, nullptr, prop_info_pod);
                  continue;
                }
              } else {
                std::cerr
                    << "Error, unexpected number of values for step range: "
                    << number_of_values << std::endl;
                continue;
              }
            } else {
              std::cerr << "Error: unsupported choice type" << std::endl;
              spa_debug_pod(4, nullptr, prop_info_pod);
              continue;
            }
          }
        }

        const auto container =
            spa_pod_find_prop(prop_info_pod, nullptr, SPA_PROP_INFO_container);
        if (container != nullptr) {
          uint32_t container_id;
          spa_pod_get_id(&container->value, &container_id);
          if (container_id == SPA_TYPE_Array) {
            prop_info.container = "array";
          } else {
            std::cerr << "Unsupported container type" << std::endl;
            spa_debug_pod(4, nullptr, prop_info_pod);
          }
        }

        const auto is_param_pod =
            spa_pod_find_prop(prop_info_pod, nullptr, SPA_PROP_INFO_params);
        if (is_param_pod != nullptr) {
          bool is_param;
          spa_pod_get_bool(&is_param_pod->value, &is_param);
          prop_info.isParam = is_param;
        }
      }
    }

    auto object = reinterpret_cast<WpPipewireObject *>(source_object);
    const char *object_serial_string =
        wp_pipewire_object_get_property(object, PW_KEY_OBJECT_SERIAL);
    const char *object_id_string =
        wp_pipewire_object_get_property(object, PW_KEY_OBJECT_ID);

    auto property_infos_json = models::prop_info::property_infos_to_json(
        strtoull(object_serial_string, nullptr, 10),
        strtoull(object_id_string, nullptr, 10), property_infos);
    this_->_prop_info_added_callback(property_infos_json->c_str());
  }
}

void Core::g_signal_object_added_callback(
    WpObjectManager *object_manager, gpointer object, gpointer user_data) {
  const auto this_ = static_cast<Core *>(user_data);

  const auto wireplumber_object = static_cast<WpPipewireObject *>(object);

  if (WP_IS_METADATA(object)) {
    this_->_metadata_collection.handle_gobject_added_event(object);
  } else {
    const auto node =
        models::objects::create_node_from_object(wireplumber_object);

    this_->trigger_object_added_callback(node);

    if (node.type == models::objects::wireplumber_object_type::node) {
      g_signal_connect(object, "params-changed",
                       G_CALLBACK(g_signal_params_changed_callback), this_);

      auto pw_object = static_cast<WpPipewireObject *>(object);
      wp_pipewire_object_enum_params(pw_object, "Props", nullptr, nullptr,
                                     wp_process_object_enum_props_callback,
                                     this_);

      wp_pipewire_object_enum_params(pw_object, "PropInfo", nullptr, nullptr,
                                     wp_process_object_enum_prop_info_callback,
                                     user_data);
    }
  }
}

void Core::g_signal_object_removed_callback(
    WpObjectManager *object_manager, gpointer object, gpointer user_data) {
  const auto this_ = static_cast<Core *>(user_data);

  const auto wireplumber_object = static_cast<WpPipewireObject *>(object);

  const char *object_serial_string =
      wp_pipewire_object_get_property(wireplumber_object, PW_KEY_OBJECT_SERIAL);

  uint64_t object_serial = strtoull(object_serial_string, nullptr, 10);

  if (WP_IS_PORT(object)) {
    this_->trigger_object_deleted_callback(
        object_serial, models::objects::wireplumber_object_type::port);
  } else if (WP_IS_LINK(object)) {
    this_->trigger_object_deleted_callback(
        object_serial, models::objects::wireplumber_object_type::link);
  } else if (WP_IS_NODE(object)) {
    this_->trigger_object_deleted_callback(
        object_serial, models::objects::wireplumber_object_type::node);
  } else if (WP_IS_DEVICE(object)) {
    this_->trigger_object_deleted_callback(
        object_serial, models::objects::wireplumber_object_type::device);
  } else if (WP_IS_CLIENT(object)) {
    this_->trigger_object_deleted_callback(
        object_serial, models::objects::wireplumber_object_type::client);
  }
}

std::ostream &operator<<(std::ostream &lhs,
                         const models::objects::wireplumber_object_type rhs) {
  switch (rhs) {
  case models::objects::wireplumber_object_type::node:
    lhs << "node";
    break;
  case models::objects::wireplumber_object_type::port:
    lhs << "port";
    break;
  case models::objects::wireplumber_object_type::link:
    lhs << "link";
    break;
  case models::objects::wireplumber_object_type::device:
    lhs << "device";
    break;
  case models::objects::wireplumber_object_type::client:
    lhs << "client";
    break;
  default:
    lhs << "unknown";
    break;
  }
  return lhs;
}

// ReSharper disable once CppMemberFunctionMayBeConst
void Core::shutdown_loop() { g_main_loop_quit(_loop); }

void Core::setup_object_manager() {
  _object_manager = wp_object_manager_new();

  wp_object_manager_add_interest(_object_manager, WP_TYPE_NODE, nullptr);
  wp_object_manager_request_object_features(_object_manager, WP_TYPE_NODE,
                                            WP_OBJECT_FEATURES_ALL);

  wp_object_manager_add_interest(_object_manager, WP_TYPE_PORT, nullptr);
  wp_object_manager_request_object_features(_object_manager, WP_TYPE_PORT,
                                            WP_OBJECT_FEATURES_ALL);

  wp_object_manager_add_interest(_object_manager, WP_TYPE_LINK, nullptr);
  wp_object_manager_request_object_features(_object_manager, WP_TYPE_LINK,
                                            WP_OBJECT_FEATURES_ALL);

  wp_object_manager_add_interest(_object_manager, WP_TYPE_DEVICE, nullptr);
  wp_object_manager_request_object_features(_object_manager, WP_TYPE_DEVICE,
                                            WP_OBJECT_FEATURES_ALL);

  wp_object_manager_add_interest(_object_manager, WP_TYPE_CLIENT, nullptr);
  wp_object_manager_request_object_features(_object_manager, WP_TYPE_CLIENT,
                                            WP_OBJECT_FEATURES_ALL);

  wp_object_manager_add_interest(_object_manager, WP_TYPE_METADATA, nullptr);
  wp_object_manager_request_object_features(_object_manager, WP_TYPE_METADATA,
                                            WP_OBJECT_FEATURES_ALL);

  g_signal_connect(_object_manager, "object-added",
                   G_CALLBACK(g_signal_object_added_callback), this);
  g_signal_connect(_object_manager, "object-removed",
                   G_CALLBACK(g_signal_object_removed_callback), this);

  wp_core_install_object_manager(_wireplumber_core, _object_manager);
}

int Core::setup_wireplumber() {
  wp_init(WP_INIT_ALL);
  _options_context = g_option_context_new("libfrwireplumber");
  _main_context = g_main_context_new();

  g_main_context_push_thread_default(_main_context);

  _loop = g_main_loop_new(_main_context, false);
  _wireplumber_core = wp_core_new(_main_context, nullptr, nullptr);
  if (!wp_core_connect(_wireplumber_core)) {
    return ERR_COULD_NOT_CONNECT_CORE;
  }

  return 0;
}

int Core::init() {
  if (const auto err = setup_wireplumber(); err != 0)
    return err;
  setup_object_manager();
  setup_pipewire();
  register_signals();
  return 0;
}

void Core::start() {
  _thread = std::make_shared<std::thread>([this]() {
    if (init() == 0) {
      run();
    }
  });
}

void Core::stop() const {
  pthread_kill(_thread->native_handle(), SIGINT);
  _thread->join();
}

void Core::run() {
  g_main_loop_run(_loop);
  cleanup();
}

void Core::cleanup() {
  g_object_unref(_object_manager);
  _object_manager = nullptr;

  wp_core_disconnect(_wireplumber_core);
  g_object_unref(_wireplumber_core);
  _wireplumber_core = nullptr;

  g_main_loop_unref(_loop);
  _loop = nullptr;

  g_main_context_unref(_main_context);
  _main_context = nullptr;

  g_option_context_free(_options_context);
  _options_context = nullptr;
}

// ReSharper disable once CppDFAUnreachableFunctionCall
void Core::trigger_object_added_callback(
    const models::objects::wireplumber_object &object) const {
  if (_object_added_callback) {
    _object_added_callback(&object);
  } else {
    std::cerr << "No object added callback set" << std::endl;
  }
}

void Core::trigger_props_changed_callback(
    const models::props::Props &props,
    const models::params::ParamUpdate &params) const {
  if (_props_changed_callback) {
    _props_changed_callback(&props, params.count > 0 ? &params : nullptr);
  } else {
    std::cerr << "No props changed callback set" << std::endl;
  }
}

void Core::trigger_props_enumeration_failed_callback(
    uint64_t object_serial) const {
  if (_props_enumeration_failed_callback) {
    _props_enumeration_failed_callback(object_serial);
  } else {
    std::cerr << "No props enumeration failed callback set" << std::endl;
  }
}

void Core::trigger_object_deleted_callback(
    uint64_t object_serial,
    models::objects::wireplumber_object_type object_type) const {
  if (_object_deleted_callback) {
    _object_deleted_callback(object_serial, object_type);
  } else {
    std::cerr << "No object deleted callback set" << std::endl;
  }
}

void Core::g_signal_params_changed_callback(WpPipewireObject *self,
                                                         const gchar *key,
                                                         gpointer user_data) {
  const auto this_ = static_cast<Core *>(user_data);
  uint64_t object_serial;
  fill_uint64(object_serial, self, PW_KEY_OBJECT_SERIAL);
  if (strcmp(key, "Props") == 0) {
    auto iterator_pointer =
        wp_pipewire_object_enum_params_sync(self, key, nullptr);
    if (iterator_pointer != nullptr) {
      g_autoptr(WpIterator) iterator = iterator_pointer;

      GValue item = G_VALUE_INIT;
      g_value_init(&item, WP_TYPE_SPA_POD);

      models::props::Props props{};
      models::params::ParamUpdate params_update{};
      while (wp_iterator_next(iterator, &item)) {
        const auto param_pod =
            static_cast<WpSpaPod *>(g_value_get_boxed(&item));
        const auto pod_type = wp_spa_pod_get_spa_type(param_pod);

        if (pod_type == SPA_TYPE_OBJECT_Props) {
          models::props::pipewire_props_fill_from_wp_spa_pod(self, param_pod,
                                                             props);

          const auto pod = wp_spa_pod_get_spa_pod(param_pod);
          const auto params_pod =
              spa_pod_find_prop(pod, nullptr, SPA_PROP_params);
          if (params_pod != nullptr) {
            models::params::fill_params_from_pod(&params_pod->value,
                                                 params_update);
          }
        }

        g_value_unset(&item);
        g_value_init(&item, WP_TYPE_SPA_POD);
      }

      this_->trigger_props_changed_callback(props, params_update);

      models::props::pipewire_props_delete(props);
      models::params::params_data_delete(params_update);
    }
  }
}

auto signal_handler = +[](gpointer user_data) -> gboolean {
  const auto _this = static_cast<Core *>(user_data);
  _this->shutdown_loop();
  return G_SOURCE_REMOVE;
};

void Core::register_signals() {
  GSource *sigint_source = g_unix_signal_source_new(SIGINT);
  g_source_set_callback(sigint_source, signal_handler, this, nullptr);
  g_source_attach(sigint_source, _main_context);
  g_source_unref(sigint_source);

  GSource *sigterm_source = g_unix_signal_source_new(SIGTERM);
  g_source_set_callback(sigterm_source, signal_handler, this, nullptr);
  g_source_attach(sigterm_source, _main_context);
  g_source_unref(sigterm_source);
}

WpPipewireObject *
Core::get_object_by_object_id(uint64_t object_id) const {
  auto proxy = wp_object_manager_lookup(_object_manager, WP_TYPE_PROXY,
                                        WP_CONSTRAINT_TYPE_G_PROPERTY,
                                        "bound-id", "=u", object_id, nullptr);

  if (proxy == nullptr) {
    std::cerr << "Could not find object with id " << object_id << std::endl;
  }

  return static_cast<WpPipewireObject *>(proxy);
}

pw_core *Core::pipewire_core() const { return _pipewire_core; }

pw_context *Core::pipewire_context() const {
  return _pipewire_context;
}

pw_loop *Core::pipewire_loop() const { return _pipewire_loop; }

void Core::setup_pipewire() {
  _pipewire_core = wp_core_get_pw_core(_wireplumber_core);
  _pipewire_context = pw_core_get_context(_pipewire_core);
  _pipewire_loop = pw_context_get_main_loop(_pipewire_context);
}

void Core::set_metadata_entry(const char *metadata_name,
                                           uint64_t subject, const char *key,
                                           const char *type,
                                           const char *value) {
  const auto metadata =
      _metadata_collection.get_metadata_by_name(metadata_name);

  if (metadata == nullptr) {
    std::cerr << "Could not find metadata for " << metadata_name << std::endl;
    return;
  }

  metadata->set_metadata_value(subject, key, type, value);
}

void Core::delete_metadata_entry(const char *metadata_name,
                                              uint64_t subject,
                                              const char *key) {
  const auto metadata =
      _metadata_collection.get_metadata_by_name(metadata_name);

  if (metadata == nullptr) {
    std::cerr << "Could not find metadata for " << metadata_name << std::endl;
    return;
  }

  metadata->delete_metadata_value(subject, key);
}

WpLink *Core::create_link_by_port_id(uint64_t output_port_id,
                                                  uint64_t input_port_id,
                                                  bool linger) const {

  const auto properties = wp_properties_new_empty();
  const auto output_port_string = std::format("{}", output_port_id);
  const auto input_port_string = std::format("{}", input_port_id);
  wp_properties_set(properties, "link.output.port", output_port_string.c_str());
  wp_properties_set(properties, "link.input.port", input_port_string.c_str());

  if (linger) {
    wp_properties_set(properties, "object.linger", "true");
  }

  WpLink *link =
      wp_link_new_from_factory(_wireplumber_core, "link-factory", properties);
  if (link == nullptr) {
    std::cerr << "Could not create link object" << std::endl;
  }

  wp_object_activate(
      WP_OBJECT(link), WP_PROXY_FEATURE_BOUND, nullptr,
      [](GObject *o, GAsyncResult *res, gpointer data) {
        GError *err = nullptr;
        WpObject *obj = WP_OBJECT(o);
        if (!wp_object_activate_finish(obj, res, &err)) {
          std::cerr << "Failed: " << (err ? err->message : "?") << "\n";
          if (err)
            g_error_free(err);
        }
      },
      nullptr);

  return link;
}

void Core::delete_link_object_id(const uint64_t object_id) const {
  const auto link = get_object_by_object_id(object_id);
  delete_link(WP_LINK(link));
}

struct delete_link_data {
  WpLink *link;
};

void Core::delete_link(WpLink *link) const {
  auto data = new delete_link_data{.link = link};

  g_main_context_invoke(
    wireplumber_context(),
    [](gpointer data) -> gboolean {
        const auto user_data = static_cast<delete_link_data *>(data);
      g_object_unref(user_data->link);
      delete user_data;
      return static_cast<gboolean>(false);
    },
    data);
}
