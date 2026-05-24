#include "../include/frsonic.h"

#include "wireplumber/models/objects/wireplumber_object.h"

#include <chrono>
#include <iostream>
#include <string_view>
#include <thread>

namespace {

void on_node_added(const wireplumber_object *node) {
  if (node == nullptr || node->type != wireplumber_object_type::node)
    return;

  const auto purpose =
      node->pmx_purpose == nullptr ? std::string_view{} : node->pmx_purpose;
  if (!purpose.starts_with("looper-"))
    return;

  std::cout << "node added: serial=" << node->object_serial
            << " name=" << (node->node_name == nullptr ? "" : node->node_name)
            << " purpose=" << purpose
            << " tag=" << (node->pmx_tag == nullptr ? "" : node->pmx_tag)
            << " media.class="
            << (node->media_class == nullptr ? "" : node->media_class)
            << std::endl;
}
void on_props_changed(const Props *, const ParamUpdate *) {}
void on_props_enum_failed(uint64_t) {}
void on_prop_info_added(const char *) {}
void on_object_deleted(uint64_t, wireplumber_object_type) {}
void on_metadata_added(const char *) {}
void on_metadata_entry_updated(const char *, uint64_t, const char *,
                               const char *, const char *) {}
void on_metadata_entry_deleted(const char *, uint64_t, const char *) {}
void on_peak(uint64_t, float, float, float, float) {}
void on_midi_cc_update(ChannelType, uint64_t, uint64_t, const char *, float,
                       float, bool) {}

bool create_looper(const frsonic_looper_config &config, size_t &handle) {
  if (frsonic_create_looper(&config, &handle)) {
    std::cout << "created looper '" << config.name << "' handle=" << handle
              << std::endl;
    return true;
  }

  std::cerr << "failed to create looper '" << config.name << "'"
            << std::endl;
  return false;
}

} // namespace

int main() {
  std::cout << "starting frsonic" << std::endl;
  frsonic_init(on_node_added, on_props_changed, on_props_enum_failed,
               on_prop_info_added, on_object_deleted, on_metadata_added,
               on_metadata_entry_updated, on_metadata_entry_deleted, on_peak,
               100, on_midi_cc_update);

  frsonic_start();
  std::cout << "frsonic started" << std::endl;

  size_t first_handle = 0;
  size_t second_handle = 0;
  const frsonic_looper_config first{
      .name = "se.test_looper.1",
      .tag = "test-looper-1",
      .description = "Sonic Eddy test looper 1",
      .capture_target_object = nullptr,
      .playback_target_object = nullptr,
      .channels = 2,
      .max_record_seconds = 300,
  };
  const frsonic_looper_config second{
      .name = "se.test_looper.2",
      .tag = "test-looper-2",
      .description = "Sonic Eddy test looper 2",
      .capture_target_object = nullptr,
      .playback_target_object = nullptr,
      .channels = 2,
      .max_record_seconds = 300,
  };

  const auto first_created = create_looper(first, first_handle);
  const auto second_created = create_looper(second, second_handle);

  std::cout << "press Enter to destroy loopers and stop" << std::endl;
  std::cin.get();

  if (first_created)
    frsonic_destroy_looper(first_handle);
  if (second_created)
    frsonic_destroy_looper(second_handle);

  frsonic_stop();
  return first_created && second_created ? 0 : 1;
}
