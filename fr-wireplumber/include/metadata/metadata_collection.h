#pragma once
#include "metadata.h"

#include <functional>
#include <memory>
#include <utility>
#include <vector>

namespace metadata {

using metadata_added_callback_t = std::function<void(const char *)>;

class MetadataCollection {
public:
  MetadataCollection(
      WireplumberThread &wireplumber_thread,
      metadata_added_callback_t metadata_added_callback,
      metadata_entry_updated_callback_t metadata_entry_updated_callback,
      metadata_entry_deleted_callback_t metadata_entry_deleted_callback)
      : _metadata_added_callback(std::move(metadata_added_callback)),
        _metadata_entry_updated_callback(
            std::move(metadata_entry_updated_callback)),
        _metadata_entry_deleted_callback(
            std::move(metadata_entry_deleted_callback)),
        _wireplumber_thread(wireplumber_thread) {}

  void handle_gobject_added_event(gpointer object);

  void add_metadata(std::string metadata_name, WpMetadata *metadata);

  MetadataPtr get_metadata_by_name(std::string metadata_name);

private:
  std::vector<MetadataPtr> _metadata_list{};
  metadata_added_callback_t _metadata_added_callback;
  metadata_entry_updated_callback_t _metadata_entry_updated_callback;
  metadata_entry_deleted_callback_t _metadata_entry_deleted_callback;
  WireplumberThread &_wireplumber_thread;
};

} // namespace metadata
