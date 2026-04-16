#pragma once

#include "controllers/ChannelType.h"
#include "controllers/MidiMix.h"

#define FR_MIDI_MAPPER_API __attribute__((visibility("default")))

using LayerSelectCallbackPtr = void (*)(size_t layer_id);

using ChannelSelectCallbackPtr = void (*)(controllers::ChannelType channel_type,
                                          size_t channel_id);

using DialSectionModeSelectCallbackPtr =
    void (*)(controllers::ChannelType channel_type, size_t channel_id,
             controllers::DialMode dial_mode);

using FilterParamsSectionSelectCallbackPtr =
    void (*)(controllers::ChannelType channel_type, size_t channel_id,
             size_t section_id);

using FilterParamsSectionMovePagesRightCallbackPtr =
    void (*)(uint64_t step_count);

using FilterParamsSectionMovePagesLeftCallbackPtr =
    void (*)(uint64_t step_count);

using MidiControlChangeUpdateCallbackPtr = void (*)(
    controllers::ChannelType channel_type, uint64_t channel_id, uint64_t object_id,
    const char *parameter_name, float normalized_controller_value,
    float normalized_known_value, bool catching_up);

extern "C" {
FR_MIDI_MAPPER_API void init(MidiControlChangeUpdateCallbackPtr callback);
FR_MIDI_MAPPER_API void start();
FR_MIDI_MAPPER_API void stop();

/*
 * Returns midi port id
 */
FR_MIDI_MAPPER_API size_t create_midi_mix_port(
    const char *pmx_purpose, const char *pmx_tag,
    LayerSelectCallbackPtr layer_select_callback,
    ChannelSelectCallbackPtr channel_select_callback,
    DialSectionModeSelectCallbackPtr dial_mode_select_callback,
    FilterParamsSectionSelectCallbackPtr filter_params_callback);

FR_MIDI_MAPPER_API size_t create_mm_1_port(
    const char *pmx_purpose, const char *pmx_tag,
    LayerSelectCallbackPtr layer_select_callback,
    ChannelSelectCallbackPtr channel_select_callback,
    DialSectionModeSelectCallbackPtr dial_mode_select_callback,
    FilterParamsSectionSelectCallbackPtr filter_params_callback,
    FilterParamsSectionMovePagesRightCallbackPtr
        filter_params_move_right_callback,
    FilterParamsSectionMovePagesLeftCallbackPtr
        filter_params_move_left_callback);

FR_MIDI_MAPPER_API size_t create_fader_fox_pc4_port(
    const char *pmx_purpose, const char *pmx_tag);

FR_MIDI_MAPPER_API void set_selected_plugin_page(size_t plugin_id,
                                                 size_t page_number);

FR_MIDI_MAPPER_API void
set_selected_channel(controllers::ChannelType channel_type, size_t channel_id);

FR_MIDI_MAPPER_API void clear_selected_channel();

FR_MIDI_MAPPER_API void set_selected_layer(size_t layer_id);

FR_MIDI_MAPPER_API void set_channel_node(controllers::ChannelType channel_type,
                                         size_t channel_id, uint64_t object_id);

FR_MIDI_MAPPER_API void set_master_channel_node(size_t layer_id,
                                                uint64_t object_id);

FR_MIDI_MAPPER_API void
set_channel_filter_node(controllers::ChannelType channel_type,
                        size_t channel_id, uint64_t object_id);

FR_MIDI_MAPPER_API void
set_channel_send_node(controllers::ChannelType channel_type, size_t channel_id,
                      size_t send_id, uint64_t object_id);

FR_MIDI_MAPPER_API void
clear_filter_parameters(controllers::ChannelType channel_type,
                        size_t channel_id);

FR_MIDI_MAPPER_API void
add_filter_parameter(controllers::ChannelType channel_type, size_t channel_id,
                     size_t plugin_id, char *name, float min, float max);
}
