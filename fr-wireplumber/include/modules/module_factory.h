#pragma once

#include "processing/WireplumberThread.h"

namespace modules::modules_factory {
bool load_module(const char *module_name, const char *module_config,
                 void **module_handle,
                 const std::shared_ptr<WireplumberThread> &wireplumber_thread);

void destroy_module(void *module_handle);
} // namespace modules::modules_factory
