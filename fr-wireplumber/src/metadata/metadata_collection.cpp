#include "metadata/metadata_collection.h"

#include <memory>

namespace metadata {
void MetadataCollection::handle_gobject_added_event(gpointer object) {
  const auto metadata = static_cast<WpMetadata *>(object);
  GValue global_properties_gvalue{0};
  g_object_get_property(G_OBJECT(metadata), "global-properties",
                        &global_properties_gvalue);

  if (global_properties_gvalue.data->v_pointer) {
    const auto global_properties =
        static_cast<WpProperties *>(global_properties_gvalue.data->v_pointer);
    if (const auto name_c =
            wp_properties_get(global_properties, "metadata.name")) {
      const std::string name(name_c);
      this->add_metadata(name, metadata);
    }
  }
}

void MetadataCollection::add_metadata(std::string metadata_name,
                                      WpMetadata *metadata) {
  if (std::ranges::find_if(
          _metadata_list, [&metadata_name](auto &inner_metadata) {
            return inner_metadata->metadata_name() == metadata_name;
          }) == _metadata_list.end()) {
    _metadata_list.emplace_back(std::make_shared<Metadata>(
        metadata_name, metadata, _wireplumber_thread,
        _metadata_entry_updated_callback, _metadata_entry_deleted_callback));
    _metadata_added_callback(metadata_name.c_str());
    _metadata_list.back()->trigger_initial_update_events();
  }
}

MetadataPtr
MetadataCollection::get_metadata_by_name(std::string metadata_name) {
  const auto metadata = std::ranges::find_if(
      _metadata_list, [&metadata_name](auto &inner_metadata) {
        return inner_metadata->metadata_name() == metadata_name;
      });

  if (metadata != _metadata_list.end()) {
    return *metadata;
  }

  return nullptr;
}
} // namespace metadata