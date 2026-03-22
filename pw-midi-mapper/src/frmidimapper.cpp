#include "frmidimapper.h"

#include "midi/Processor.h"

#include <memory>

std::shared_ptr<midi::Processor> g_processor = nullptr;

void init() { g_processor = std::make_shared<midi::Processor>(); }

void start() { g_processor->start(); }

void stop() { g_processor->stop(); }

size_t create_midi_mix_port(
    const char *pmx_purpose, const char *pmx_tag,
    LayerSelectCallbackPtr layer_select_callback,
    ChannelSelectCallbackPtr channel_select_callback,
    DialSectionModeSelectCallbackPtr dial_mode_select_callback,
    FilterParamsSectionSelectCallbackPtr filter_params_callback) {
  const auto id = g_processor->create_midi_mix_port(
      pmx_purpose, pmx_tag, layer_select_callback, channel_select_callback,
      dial_mode_select_callback, filter_params_callback);

  if (!id) {
    throw std::logic_error("Could not create midi mix port");
  }

  return *id;
}

size_t
create_mm_1_port(const char *pmx_purpose, const char *pmx_tag,
                 LayerSelectCallbackPtr layer_select_callback,
                 ChannelSelectCallbackPtr channel_select_callback,
                 DialSectionModeSelectCallbackPtr dial_mode_select_callback,
                 FilterParamsSectionSelectCallbackPtr filter_params_callback,
                 FilterParamsSectionMovePagesRightCallbackPtr
                     filter_params_move_right_callback,
                 FilterParamsSectionMovePagesLeftCallbackPtr
                     filter_params_move_left_callback) {

  const auto id = g_processor->create_mm_1_port(
      pmx_purpose, pmx_tag, layer_select_callback, channel_select_callback,
      dial_mode_select_callback, filter_params_callback,
      filter_params_move_right_callback, filter_params_move_left_callback);

  if (!id) {
    throw std::logic_error("Could not create midi mix port");
  }

  return *id;
}
