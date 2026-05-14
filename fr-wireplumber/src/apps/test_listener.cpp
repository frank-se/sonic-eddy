#include <iostream>
#include <spa/debug/pod.h>
#include <thread>

#include "frwireplumber.h"
#include "models/objects/wireplumber_object.h"
#include "processing/WireplumberThread.h"

void test_object_added_callback(
    const models::objects::wireplumber_object *node) {
  /*
  if (node->type == wireplumber_object_type::node) {
    std::cout << "Object found, serial: " << node->object_serial << std::endl;
  }
  */
}

void test_props_changed_callback(
    const models::props::Props *props,
    const models::params::ParamUpdate *param_update) {
  std::cout << "test_props_changed_callback" << std::endl;
  /*
  std::cout << "Props for object serial changed: " << props->object_serial <<
    std::endl;

  for (size_t i = 0; i < props->channel_map_size; i++) {
    std::cout << "Channel " << i << " is " << props->channel_map[i] <<
      std::endl;
  }

  for (size_t i = 0; i < props->channel_volumes_size; i++) {
    std::cout << "Channel " << i << " volume is " << props->channel_volumes[i]
      << std::endl;
  }
  */
}

void test_prop_info_added_callback(const char *prop_infos_json) {
  // std::cout << prop_infos_json << std::endl;
}

void test_object_delected_callback(
    uint64_t object_serial,
    models::objects::wireplumber_object_type object_type) {};

void test_metadata_added_callback(const char *metadata_name) {
  /*
  std::cout << "test_metadata_added_callback" << std::endl;
  std::cout << metadata_name << std::endl;
  */
};

void test_metadata_entry_updated_callback(const char *metadata_name,
                                          unsigned long subject,
                                          const char *key, const char *type,
                                          const char *value) {
  /*
  std::cout << "test_metadata_entry_updated_callback" << std::endl;
  std::cout << metadata_name << " " << key << " " << type << " " << value
            << std::endl;
            */
};

void test_metadata_entry_deleted_callback(const char *metadata_name,
                                          unsigned long subject,
                                          const char *key) {
  /*
  std::cout << "test_metadata_entry_deleted_callback" << std::endl;
  std::cout << metadata_name << " " << key << " " << std::endl;
  */
};

void test_props_enum_failed_callback(uint64_t object_serial) {
  std::cout << "test_props_enum_failed_callback for " << object_serial
            << std::endl;
}

int main() {
  init(test_object_added_callback, test_props_changed_callback,
       test_props_enum_failed_callback, test_prop_info_added_callback,
       test_object_delected_callback, test_metadata_added_callback,
       test_metadata_entry_updated_callback,
       test_metadata_entry_deleted_callback);

  start();

  // sleep(5);
  /*
  std::cout << "loading module" << std::endl;

  auto config = std::format(R"({{
      node.description = "{}"
      capture.props = {{
        node.name = "{}-capture"
        media.class = "Stream/Input/Audio"
        audio.position = [ AUX{} AUX{} ]
        target.object = "{}"
        node.autoconnect = false
      }},
      "playback.props": {{
        node.name = "{}-playback"
        audio.position = [ FL FR ]
        media.class = "Stream/Output/Audio"
        node.passive = false
      }}
    }})", "my-test-loopback", "my-test-loopback", 0, 1,
                            "alsa_input.pci-0000_04_00.0.pro-input-0",
                            "my-test-loopback");

  std::string module_name("libpipewire-module-loopback");

  void *module_handle = nullptr;
  load_module(module_name.c_str(), config.c_str(), &module_handle);
*/

  sleep(1);

  auto link = create_link_by_port_ids(61, 58, false);

  sleep(3);

  std::cout << "delete link" << std::endl;
  delete_link(link);


  // set_mute(206, false);

  // double volumes[2] = {1.0, 1.0};
  // set_volumes(209, volumes, 2);
  //  sleep(2);
  //  set_mute(206, false);

  /*
  std::cout << "set metadata" << std::endl;

  set_metadata_entry("default", 0, "test", "Spa:String:JSON", "test");
  sleep(10);

  std::cout << "delete metadata" << std::endl;

  delete_metadata_entry("default", 0, "test");
  /*
  std::cout << "destroying module " << std::endl;
  destroy_module(module_handle);
  std::cout << "Setting volume to 0.8" << std::endl;
  double volume[2] = { 0.8, 0.8 };
  set_volume(275, volume, 2);
   */

  sleep(5000);

  return 0;
}
