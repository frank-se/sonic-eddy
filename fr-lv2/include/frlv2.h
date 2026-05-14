#pragma once

#define FR_LV2_API __attribute__((visibility("default")))

extern "C" {
FR_LV2_API void init();
FR_LV2_API void destroy();
FR_LV2_API const char *plugin_descriptions_json();
FR_LV2_API const char *plugin_classes_json();
}