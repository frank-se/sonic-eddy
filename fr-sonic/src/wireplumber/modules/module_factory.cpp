#include "core/Core.h"
#include "modules/module_factory.h"

#include <iostream>
#include <memory>
#include <ostream>
#include <pipewire/impl-module.h>
#include <pipewire/pipewire.h>
#include <string>

struct load_module_pw_invoke_data {
  std::string config;
  std::string module_name;
  pw_context *context;
  pw_impl_module *module = nullptr;
};

bool modules::modules_factory::load_module(
    const char *module_name, const char *module_config, void **module_handle,
    const std::shared_ptr<Core> &wireplumber_thread) {
  const auto pw_context = wireplumber_thread->pipewire_context();
  const auto pw_main_loop = wireplumber_thread->pipewire_loop();

  load_module_pw_invoke_data module_data{.config = module_config,
                                         .module_name = module_name,
                                         .context = pw_context};

  pw_loop_invoke(
      pw_main_loop,
      [](spa_loop *loop, bool async, std::uint32_t seq, const void *data,
         size_t size, void *user_data) {
        const auto module_data =
            static_cast<load_module_pw_invoke_data *>(user_data);

        const auto module = pw_context_load_module(
            module_data->context, module_data->module_name.c_str(),
            module_data->config.c_str(), nullptr);

        module_data->module = module;
        return 0;
      },
      0, nullptr, 0, true, &module_data);

  if (module_data.module) {
    *module_handle = static_cast<void *>(module_data.module);
    return true;
  } else {
    std::cerr << "Failed to load module: " << module_data.module << std::endl;
    return false;
  }
}

void modules::modules_factory::destroy_module(void *module_handle) {
  const auto module = static_cast<pw_impl_module *>(module_handle);
  pw_impl_module_schedule_destroy(module);
}
