#include "metadata//metadata.h"
#include "core/Core.h"

#include <condition_variable>
#include <iostream>
#include <ostream>

void metadata::Metadata::trigger_initial_update_events() {
  std::lock_guard lock(_no_parallel_calls_mutex);

  struct metadata_result_t {
    unsigned long subject;
    std::string key;
    std::string type;
    std::string value;
  };

  struct user_data_t {
    WpMetadata *metadata;
    Metadata *metadata_item;
  };

  auto user_data = user_data_t{_metadata, this};

  g_main_context_invoke(
      _wireplumber_thread.wireplumber_context(),
      [](gpointer user_data) {
        auto *data = static_cast<user_data_t *>(user_data);

        auto metadata_iterator = wp_metadata_new_iterator(data->metadata, 0);
        GValue value = {0};
        while (wp_iterator_next(metadata_iterator, &value)) {
          auto metadata_item =
              static_cast<WpMetadataItem *>(value.data->v_pointer);
          const auto key = wp_metadata_item_get_key(metadata_item);
          const auto metadata_value = wp_metadata_item_get_value(metadata_item);
          const auto type = wp_metadata_item_get_value_type(metadata_item);
          const auto subject = wp_metadata_item_get_subject(metadata_item);
          data->metadata_item->trigger_metadata_entry_updated(
              subject, key, type, metadata_value);
        }

        return static_cast<gboolean>(false);
      },
      &user_data);
}

void metadata::Metadata::set_metadata_value(const uint64_t subject,
                                            const std::string &key,
                                            const std::string &type,
                                            const std::string &value) {
  std::lock_guard lock(_no_parallel_calls_mutex);

  struct user_data_t {
    WpMetadata *metadata;
    uint64_t subject;
    std::string key;
    std::string type;
    std::string value;
  };

  const auto user_data = new user_data_t{_metadata, subject, key, type, value};

  g_main_context_invoke(
      _wireplumber_thread.wireplumber_context(),
      [](gpointer user_data) {
        auto *data = static_cast<user_data_t *>(user_data);
        wp_metadata_set(data->metadata, data->subject, data->key.c_str(),
                        "Spa:String:JSON", data->value.c_str());
        delete data;
        return static_cast<gboolean>(false);
      },
      user_data);
}

void metadata::Metadata::delete_metadata_value(uint64_t subject,
                                               const std::string &key) {
  std::lock_guard lock(_no_parallel_calls_mutex);

  struct user_data_t {
    WpMetadata *metadata;
    uint64_t subject;
    std::string key;
  };

  const auto user_data = new user_data_t{_metadata, subject, key};

  g_main_context_invoke(
      _wireplumber_thread.wireplumber_context(),
      [](gpointer user_data) {
        const auto *data = static_cast<user_data_t *>(user_data);
        wp_metadata_set(data->metadata, data->subject, data->key.c_str(),
                        nullptr, nullptr);
        delete data;
        return static_cast<gboolean>(false);
      },
      user_data);
}

void metadata::Metadata::trigger_metadata_entry_updated(
    const unsigned long subject, const std::string &key,
    const std::string &type, const std::string &value) const {
  _metadata_entry_updated_callback(_metadata_name.c_str(), subject, key.c_str(),
                                   type.c_str(), value.c_str());
}

void metadata::Metadata::trigger_metadata_entry_deleted(
    const unsigned long subject, const std::string &key) const {
  _metadata_entry_deleted_callback(_metadata_name.c_str(), subject,
                                   key.c_str());
}

namespace metadata {
auto metadata_changed_callback = [](WpMetadata *, const guint subject,
                                    const gchar *key, const gchar *type,
                                    const gchar *value, gpointer user_data) {
  const auto metadata = static_cast<Metadata *>(user_data);
  if (value != nullptr) {
    metadata->trigger_metadata_entry_updated(subject, key, type, value);
  } else {
    if (key == nullptr) return;

    metadata->trigger_metadata_entry_deleted(subject, key);
  }
};
} // namespace metadata

void metadata::Metadata::initialize() {
  g_signal_connect(_metadata, "changed", G_CALLBACK(+metadata_changed_callback),
                   this);
}
