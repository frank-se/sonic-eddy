#pragma once

#include "Callbacks.h"
#include "action_container/IActionContainer.h"
#include "controllers/Channel.h"
#include "midi/Sender.h"

#include "pipewire/pipewire.h"

#include <memory>
#include <utility>

namespace controllers {

class CmdMm1 : public action_container::IActionContainer {
public:
  CmdMm1(pw_registry *registry, pw_main_loop *loop,
         const midi::SenderPointer &feedback_channel,
         const LayerSelectCallback &layer_select_callback,
         const ChannelSelectCallback &channel_select_callback,
         const DialSectionModeCallback &dial_section_mode_callback,
         const FilterParamsSectionSelectCallback
             &filter_params_section_select_callback,
         const FilterParamsSectionMovePagesRightCallback
             &filter_params_pages_right_callback,
         const FilterParamsSectionMovePagesLeftCallback
             &filter_params_pages_left_callback)
      : _registry(registry), _loop(loop), _feedback_channel(feedback_channel),
        _layer_select_callback(layer_select_callback),
        _channel_select_callback(channel_select_callback),
        _dial_section_mode_callback(dial_section_mode_callback),
        _filter_params_section_select_callback(
            filter_params_section_select_callback),
        _filter_params_pages_right_callback(filter_params_pages_right_callback),
        _filter_params_pages_left_callback(filter_params_pages_left_callback) {}

  void process(const midi::Message &message) override;

private:
  pw_registry *_registry;
  pw_main_loop *_loop;
  midi::SenderPointer _feedback_channel;

  LayerSelectCallback _layer_select_callback;
  ChannelSelectCallback _channel_select_callback;
  DialSectionModeCallback _dial_section_mode_callback;
  FilterParamsSectionSelectCallback _filter_params_section_select_callback;
  FilterParamsSectionMovePagesRightCallback _filter_params_pages_right_callback;
  FilterParamsSectionMovePagesLeftCallback _filter_params_pages_left_callback;

  /*
   * We always have 8 channels, each assigned to a layer. The first 4 are
   * assigned to layer 0, and the second 4 are assigned to layer 1.
   */
  std::array<Channel, 8> _channels{};

  static constexpr uint8_t _minimum_vu = 48u;
  static constexpr uint8_t _maximum_vu = 63u;
  static constexpr uint8_t _left_vu_meter_index = 80u;
  static constexpr uint8_t _right_vu_meter_index = 81u;

  static constexpr uint8_t _button_feedback_off = 1u;
  static constexpr uint8_t _button_feedback_on_solid = 2u;
  static constexpr uint8_t _button_feedback_on_blinking = 3u;

  static constexpr uint32_t _encoder_left = 2113929216u;
  static constexpr uint32_t _encoder_right = 2181570690u;
};

} // namespace controllers