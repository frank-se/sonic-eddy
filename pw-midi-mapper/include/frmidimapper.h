#pragma once

#include <memory>

#define FR_MIDI_MAPPER_API __attribute__((visibility("default")))

#include "controllers/MidiMix.h"

using NoteOnCallbackPtr = void (*)(size_t midi_port_id, size_t mapping_id,
                                   uint64_t note_number, uint64_t velocity);

using LayerSelectCallbackPtr = void (*)(size_t midi_port_id, size_t layer_id);

using ChannelSelectCallbackPtr = void (*)(size_t midi_port_id,
                                          size_t channel_id);

using DialSectionModeSelectCallbackPtr = void (*)(
    size_t midi_port_id, size_t channel_id, controllers::DialMode dial_mode);

using FilterParamsSectionSelectCallbackPtr = void (*)(size_t midi_port_id,
                                                      size_t channel_id,
                                                      size_t section_id);

using FilterParamsSectionMovePagesRightCallbackPtr =
    void (*)(size_t midi_port_id, uint64_t step_count);

using FilterParamsSectionMovePagesLeftCallbackPtr =
    void (*)(size_t midi_port_id, uint64_t step_count);

extern "C" {
FR_MIDI_MAPPER_API void init();
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

FR_MIDI_MAPPER_API size_t
create_mm_1_port(const char *pmx_purpose, const char *pmx_tag,
                 LayerSelectCallbackPtr layer_select_callback,
                 ChannelSelectCallbackPtr channel_select_callback,
                 DialSectionModeSelectCallbackPtr dial_mode_select_callback,
                 FilterParamsSectionSelectCallbackPtr filter_params_callback,
                 FilterParamsSectionMovePagesRightCallbackPtr
                     filter_params_move_right_callback,
                 FilterParamsSectionMovePagesLeftCallbackPtr
                     filter_params_move_left_callback);
}
