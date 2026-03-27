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
  logging::log<logging::LogLevel::Trace>("create_midi_mix_port");

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
  logging::log<logging::LogLevel::Trace>("create_mm_1_port");

  const auto id = g_processor->create_mm_1_port(
      pmx_purpose, pmx_tag, layer_select_callback, channel_select_callback,
      dial_mode_select_callback, filter_params_callback,
      filter_params_move_right_callback, filter_params_move_left_callback);

  if (!id) {
    throw std::logic_error("Could not create midi mix port");
  }

  return *id;
}

size_t create_fader_fox_pc4_port(const char *pmx_purpose, const char *pmx_tag) {
  logging::log<logging::LogLevel::Trace>("create_fader_fox_pc4_port");

  const auto id = g_processor->create_fader_fox_pc4_port(pmx_purpose, pmx_tag);

  if (!id) {
    throw std::logic_error("Could not create faderfox port");
  }

  return *id;
}

void set_selected_plugin_page(const size_t plugin_id,
                              const size_t page_number) {
  g_processor->set_selected_plugin_page(plugin_id, page_number);
}

void set_selected_channel(const controllers::ChannelType channel_type,
                          const size_t channel_id) {
  g_processor->set_selected_channel(channel_type, channel_id);
}

void set_selected_layer(const size_t layer_id) {
  g_processor->set_selected_layer(layer_id);
}
