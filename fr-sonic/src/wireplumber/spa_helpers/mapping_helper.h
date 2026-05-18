#pragma once

#include <wp/wp.h>

inline void fill_bool(bool &value, WpPipewireObject *object, const char *key) {
  const char *props = nullptr;
  if ((props = wp_pipewire_object_get_property(object, key)) != nullptr) {
    value = (strcmp(props, "true") == 0);
  }
}

inline void fill_double(double &value, WpPipewireObject *object,
                        const char *key) {
  const char *props = nullptr;
  if ((props = wp_pipewire_object_get_property(object, key)) != nullptr) {
    value = strtod(props, nullptr);
  }
}

inline void fill_uint64(uint64_t &value, WpPipewireObject *object,
                        const char *key) {
  const char *props = nullptr;
  if ((props = wp_pipewire_object_get_property(object, key)) != nullptr) {
    value = strtoull(props, nullptr, 10);
  }
}

inline void fill_uint64_with_existence(uint64_t &value, bool &exists,
                                       WpPipewireObject *object,
                                       const char *key) {
  const char *props = nullptr;
  if ((props = wp_pipewire_object_get_property(object, key)) != nullptr) {
    value = strtoull(props, nullptr, 10);
    exists = true;
  } else {
    exists = false;
  }
}

inline const char *read_string(WpPipewireObject *object, const char *key) {
  const char *props = nullptr;
  if ((props = wp_pipewire_object_get_property(object, key)) != nullptr) {
    return props;
  }
  return nullptr;
}
