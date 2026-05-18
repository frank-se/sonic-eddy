#pragma once

#include <functional>
#include <memory>
#include <mutex>
#include <string>
#include <utility>

#include <wp/wp.h>

class Core;

namespace metadata {

using metadata_entry_updated_callback_t = std::function<void(
    const char *, unsigned long, const char *, const char *, const char *)>;

using metadata_entry_deleted_callback_t =
    std::function<void(const char *, unsigned long, const char *)>;

class Metadata {
public:
  Metadata(const std::string &metadata_name, WpMetadata *metadata,
           Core &wireplumber_thread,
           metadata_entry_updated_callback_t metadata_entry_updated_callback,
           metadata_entry_deleted_callback_t metadata_entry_deleted_callback)
      : _metadata_name(metadata_name), _metadata(metadata),
        _metadata_entry_updated_callback(
            std::move(metadata_entry_updated_callback)),
        _metadata_entry_deleted_callback(
            std::move(metadata_entry_deleted_callback)),
        _wireplumber_thread(wireplumber_thread) {
    initialize();
  };

  void initialize();

  void trigger_initial_update_events();

  void set_metadata_value(uint64_t subject, const std::string &key,
                          const std::string &type, const std::string &value);

  void delete_metadata_value(uint64_t subject, const std::string &key);

  void trigger_metadata_entry_updated(unsigned long subject,
                                      const std::string &key,
                                      const std::string &type,
                                      const std::string &value) const;

  void trigger_metadata_entry_deleted(unsigned long subject,
                                      const std::string &key) const;

  [[nodiscard]] const std::string &metadata_name() const {
    return _metadata_name;
  };

private:
  std::string _metadata_name;
  std::mutex _no_parallel_calls_mutex;
  WpMetadata *_metadata = nullptr;
  metadata_entry_updated_callback_t _metadata_entry_updated_callback = nullptr;
  metadata_entry_deleted_callback_t _metadata_entry_deleted_callback = nullptr;
  Core &_wireplumber_thread;
};

using MetadataPtr = std::shared_ptr<Metadata>;

} // namespace metadata
