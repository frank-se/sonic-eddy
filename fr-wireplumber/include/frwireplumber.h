#pragma once

#include "models/objects/wireplumber_object.h"
#include "models/props/props.h"

#include <wp/link.h>

#define FR_WIREPLUMBER_API __attribute__((visibility("default")))

using node_added_callback_pointer_t =
    void (*)(const models::objects::wireplumber_object *node);

using props_changed_callback_pointer_t =
    void (*)(const models::props::Props *props,
             const models::params::ParamUpdate *param_update);

using props_enum_failed_callback_pointer_t = void (*)(uint64_t object_serial);

using prop_info_added_callback_pointer_t =
    void (*)(const char *property_infos_json);

using object_deleted_callback_pointer_t = void (*)(
    uint64_t object_serial, models::objects::wireplumber_object_type type);

using metadata_added_callback_pointer_t = void (*)(const char *metadata_name);

using metadata_entry_updated_callback_pointer_t =
    void (*)(const char *metadata_name, uint64_t subject, const char *key,
             const char *type, const char *value);

using metadata_entry_deleted_callback_pointer_t =
    void (*)(const char *metadata_name, uint64_t subject, const char *key);

extern "C" {
/*
 * Lifetime Management
 */
FR_WIREPLUMBER_API void
init(node_added_callback_pointer_t node_added_callback,
     props_changed_callback_pointer_t props_changed_callback,
     props_enum_failed_callback_pointer_t props_enum_failed_callback,
     prop_info_added_callback_pointer_t prop_info_added_callback,
     object_deleted_callback_pointer_t object_deleted_callback,
     metadata_added_callback_pointer_t metadata_added_callback,
     metadata_entry_updated_callback_pointer_t metadata_entry_updated_callback,
     metadata_entry_deleted_callback_pointer_t metadata_entry_deleted_callback);

FR_WIREPLUMBER_API void start();
FR_WIREPLUMBER_API void stop();

/*
 * Pipewire Object Property Handling
 */
FR_WIREPLUMBER_API void set_volumes(uint64_t object_id, const double *volume,
                                    uint64_t count);

FR_WIREPLUMBER_API void set_mute(uint64_t object_id, bool mute);

FR_WIREPLUMBER_API void set_params(uint64_t object_id, char **keys,
                                   models::params::ParamType *param_types,
                                   uint64_t *values, size_t count);

/*
 * Pipewire Module Handling
 */
FR_WIREPLUMBER_API bool load_module(const char *module_name,
                                    const char *module_config,
                                    void **module_handle);

FR_WIREPLUMBER_API void destroy_module(void *module_handle);

/*
 * Metadata handling
 */
FR_WIREPLUMBER_API void set_metadata_entry(const char *metadata_name,
                                           uint64_t subject, const char *key,
                                           const char *type, const char *value);

FR_WIREPLUMBER_API void delete_metadata_entry(const char *metadata_name,
                                              uint64_t subject,
                                              const char *key);

/*
 * Link Management
 */
FR_WIREPLUMBER_API WpLink *create_link_by_port_ids(uint64_t output_port_id,
                                                   uint64_t input_port_id,
                                                   bool linger);

FR_WIREPLUMBER_API void delete_link_by_object_id(uint64_t object_id);

FR_WIREPLUMBER_API void delete_link(WpLink *link);

} // extern "C"
