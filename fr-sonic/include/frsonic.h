#pragma once

#include <cstddef>
#include <cstdint>

#define FRSONIC_API __attribute__((visibility("default")))

/* ── internal type aliases ───────────────────────────────────────────────── */

/* Forward-declare internal types in their actual namespaces so callback
   signatures here match Core/Processor exactly — no casts needed at the
   boundary layer. */
namespace models::objects {
struct wireplumber_object;
enum class wireplumber_object_type : int;
} // namespace models::objects
namespace models::props {
struct Props;
}
namespace models::params {
struct ParamUpdate;
enum class ParamType;
} // namespace models::params

using models::objects::wireplumber_object;
using models::objects::wireplumber_object_type;
using models::params::ParamUpdate;
using models::props::Props;

/* ── callback types ──────────────────────────────────────────────────────── */

using node_added_callback_t = void (*)(const wireplumber_object *node);

using props_changed_callback_t = void (*)(const Props *props,
                                          const ParamUpdate *param_update);

using props_enum_failed_callback_t = void (*)(uint64_t object_serial);

using prop_info_added_callback_t = void (*)(const char *property_infos_json);

using object_deleted_callback_t = void (*)(uint64_t object_serial,
                                           wireplumber_object_type type);

using metadata_added_callback_t = void (*)(const char *metadata_name);

using metadata_entry_updated_callback_t = void (*)(const char *metadata_name,
                                                   uint64_t subject,
                                                   const char *key,
                                                   const char *type,
                                                   const char *value);

using metadata_entry_deleted_callback_t = void (*)(const char *metadata_name,
                                                   uint64_t subject,
                                                   const char *key);

/* monitoring */
using peak_callback_t = void (*)(uint64_t object_serial, float left_peak,
                                 float right_peak, float left_average,
                                 float right_average);

/* midi — ChannelType / DialMode kept as independent enums matching C# values */
enum class ChannelType : int { Channel = 0, GroupChannel = 1, Master = 2 };
enum class DialMode : int { Sends = 1, FilterParams = 2 };

using layer_select_callback_t = void (*)(size_t layer_id);
using channel_select_callback_t = void (*)(ChannelType channel_type,
                                           size_t channel_id);
using dial_mode_callback_t = void (*)(ChannelType channel_type,
                                      size_t channel_id, DialMode dial_mode);
using filter_section_callback_t = void (*)(ChannelType channel_type,
                                           size_t channel_id,
                                           size_t section_id);
using pages_right_callback_t = void (*)(uint64_t step_count);
using pages_left_callback_t = void (*)(uint64_t step_count);
using midi_cc_update_callback_t =
    void (*)(ChannelType channel_type, uint64_t channel_id, uint64_t object_id,
             const char *parameter_name, float normalized_value,
             float normalized_known_value, bool catching_up);
using launchpad_loop_pressed_callback_t =
    void (*)(size_t layer_id, ChannelType channel_type, size_t channel_id,
             size_t loop_position);
using launchpad_fader_changed_callback_t =
    void (*)(size_t layer_id, ChannelType channel_type, size_t channel_id,
             float normalized_value);

struct frsonic_looper_config {
  const char *name;
  const char *tag;
  const char *description;
  const char *capture_target_object;
  const char *playback_target_object;
  const char *archive_folder_path;
  uint32_t channels;
  uint32_t max_record_seconds;
  float mix;
  /* optional comma-separated channel names, e.g. "AUX0,AUX1" – null = default */
  const char *playback_audio_position;
};

struct frsonic_midi_manipulator_config {
  const char *name;
  const char *tag;
  const char *description;
};

extern "C" {

/* ── lifecycle ───────────────────────────────────────────────────────────── */
FRSONIC_API void
frsonic_init(node_added_callback_t node_added,
             props_changed_callback_t props_changed,
             props_enum_failed_callback_t props_enum_failed,
             prop_info_added_callback_t prop_info_added,
             object_deleted_callback_t object_deleted,
             metadata_added_callback_t metadata_added,
             metadata_entry_updated_callback_t metadata_entry_updated,
             metadata_entry_deleted_callback_t metadata_entry_deleted,
             peak_callback_t peak, uint64_t peak_update_interval_ms,
             midi_cc_update_callback_t midi_cc_update);
FRSONIC_API void frsonic_start();
FRSONIC_API void frsonic_stop();

/* ── loopers ─────────────────────────────────────────────────────────────── */
FRSONIC_API bool frsonic_create_looper(const frsonic_looper_config *config,
                                       size_t *out_handle);
FRSONIC_API void frsonic_destroy_looper(size_t looper_handle);

/* ── MIDI manipulators ──────────────────────────────────────────────────── */
FRSONIC_API bool
frsonic_create_midi_manipulator(const frsonic_midi_manipulator_config *config,
                                size_t *out_handle);
FRSONIC_API void frsonic_destroy_midi_manipulator(size_t manipulator_handle);

/* ── wireplumber / object model ──────────────────────────────────────────── */
FRSONIC_API void frsonic_set_volumes(uint64_t object_id, const double *volumes,
                                     size_t count);
FRSONIC_API void frsonic_set_mute(uint64_t object_id, bool mute);
FRSONIC_API void frsonic_set_params(uint64_t object_id, char **keys,
                                    models::params::ParamType *param_types,
                                    uint64_t *values, size_t count);
FRSONIC_API bool frsonic_load_module(const char *name, const char *config,
                                     void **handle);
FRSONIC_API void frsonic_destroy_module(void *handle);
FRSONIC_API void frsonic_set_metadata_entry(const char *metadata_name,
                                            uint64_t subject, const char *key,
                                            const char *type,
                                            const char *value);
FRSONIC_API void frsonic_delete_metadata_entry(const char *metadata_name,
                                               uint64_t subject,
                                               const char *key);
FRSONIC_API void frsonic_create_link_by_port_ids(uint64_t out_port_id,
                                                 uint64_t in_port_id,
                                                 bool linger);
FRSONIC_API void frsonic_delete_link_by_object_id(uint64_t object_id);

/* ── monitoring ──────────────────────────────────────────────────────────── */
FRSONIC_API void frsonic_start_monitor_node(uint64_t object_serial);
FRSONIC_API void frsonic_stop_monitor_node(uint64_t object_serial);

/* ── midi ────────────────────────────────────────────────────────────────── */
FRSONIC_API size_t frsonic_create_midi_mix_port(
    const char *pmx_purpose, const char *pmx_tag,
    layer_select_callback_t layer_cb, channel_select_callback_t channel_cb,
    dial_mode_callback_t dial_mode_cb,
    filter_section_callback_t filter_section_cb);

FRSONIC_API size_t frsonic_create_mm1_port(
    const char *pmx_purpose, const char *pmx_tag,
    layer_select_callback_t layer_cb, channel_select_callback_t channel_cb,
    dial_mode_callback_t dial_mode_cb,
    filter_section_callback_t filter_section_cb,
    pages_right_callback_t pages_right_cb, pages_left_callback_t pages_left_cb);

FRSONIC_API size_t frsonic_create_fader_fox_pc4_port(const char *pmx_purpose,
                                                     const char *pmx_tag);

FRSONIC_API size_t frsonic_create_launchpad_mini_port(
    const char *pmx_purpose, const char *pmx_tag,
    layer_select_callback_t layer_cb,
    launchpad_loop_pressed_callback_t loop_pressed_cb,
    launchpad_fader_changed_callback_t fader_changed_cb);

FRSONIC_API void frsonic_set_selected_plugin_page(size_t plugin_id,
                                                  size_t page_number);
FRSONIC_API void frsonic_set_selected_channel(ChannelType channel_type,
                                              size_t channel_id);
FRSONIC_API void frsonic_clear_selected_channel();
FRSONIC_API void frsonic_set_selected_layer(size_t layer_id);
FRSONIC_API void frsonic_set_channel_node(ChannelType channel_type,
                                          size_t channel_id,
                                          uint64_t object_id);
FRSONIC_API void frsonic_set_master_channel_node(size_t layer_id,
                                                 uint64_t object_id);
FRSONIC_API void frsonic_set_channel_filter_node(ChannelType channel_type,
                                                 size_t channel_id,
                                                 uint64_t object_id);
FRSONIC_API void frsonic_set_channel_send_node(ChannelType channel_type,
                                               size_t channel_id,
                                               size_t send_id,
                                               uint64_t object_id);
FRSONIC_API void frsonic_clear_filter_parameters(ChannelType channel_type,
                                                 size_t channel_id);
FRSONIC_API void frsonic_add_filter_parameter(ChannelType channel_type,
                                              size_t channel_id,
                                              size_t plugin_id, char *name,
                                              float min, float max);
FRSONIC_API void frsonic_set_launchpad_mini_looper_slot_state(
    ChannelType channel_type, size_t channel_id, size_t loop_position,
    bool available, bool loaded, bool playing);

/* ── lv2 ─────────────────────────────────────────────────────────────────── */
FRSONIC_API void frsonic_lv2_init();
FRSONIC_API void frsonic_lv2_destroy();
FRSONIC_API const char *frsonic_lv2_plugin_descriptions_json();
FRSONIC_API const char *frsonic_lv2_plugin_classes_json();

/* ── silence producers ───────────────────────────────────────────────────── */
FRSONIC_API void *frsonic_create_silence_producer(uint64_t target_serial);
FRSONIC_API void frsonic_destroy_silence_producer(void *handle);

} // extern "C"
