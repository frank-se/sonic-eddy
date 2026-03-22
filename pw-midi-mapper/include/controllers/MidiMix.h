#pragma once

#include "Callbacks.h"
#include "DialColumnAndRow.h"
#include "action_container/IActionContainer.h"
#include "controllers/Channel.h"
#include "midi/Messages.h"
#include "midi/Sender.h"
#include "pw_utils/SetParamsData.h"
#include "registry/Registry.h"

#include <functional>
#include <utility>

namespace controllers {

class MidiMix : public action_container::IActionContainer {
public:
  MidiMix(
      registry::Registry &registry, pw_main_loop *loop,
      midi::SenderPointer feedback_channel,
      LayerSelectCallback layer_select_callback,
      ChannelSelectCallback channel_select_callback,
      DialSectionModeCallback dial_section_mode_select_callback,
      FilterParamsSectionSelectCallback filter_params_section_select_callback)
      : _registry(registry), _loop(loop),
        _feedback_channel(std::move(feedback_channel)),
        _layer_select_callback(std::move(layer_select_callback)),
        _channel_select_callback(std::move(channel_select_callback)),
        _dial_section_mode_select_callback(
            std::move(dial_section_mode_select_callback)),
        _filter_params_section_select_callback(
            std::move(filter_params_section_select_callback)) {}

  void process(const midi::Message &message) override;

  void set_channel_playback_node(size_t channel_id, uint64_t object_id);
  void set_channel_filter_node(size_t channel_id, uint64_t object_id);
  void set_send_node(size_t channel_id, size_t send_id, uint64_t object_id);

private:
  static constexpr size_t _channels_per_layer{8};

  registry::Registry &_registry;
  pw_main_loop *_loop;
  midi::SenderPointer _feedback_channel;

  std::atomic<size_t> _selected_layer_id{0};
  std::atomic<std::optional<size_t>> _selected_channel_id{std::nullopt};

  registry::Node *_master_channel_playback_node = nullptr;

  /*
   * We always have 16 channels, each assigned to a layer. The first 8 are
   * assigned to layer 0, and the second 8 are assigned to layer 1.
   */
  std::array<Channel, 16> _channels{
      Channel(0),  Channel(1),  Channel(2),  Channel(3),
      Channel(4),  Channel(5),  Channel(6),  Channel(7),
      Channel(8),  Channel(9),  Channel(10), Channel(11),
      Channel(12), Channel(13), Channel(14), Channel(15),
  };

  LayerSelectCallback _layer_select_callback;
  ChannelSelectCallback _channel_select_callback;
  DialSectionModeCallback _dial_section_mode_select_callback;
  FilterParamsSectionSelectCallback _filter_params_section_select_callback;

  void handle_note_on(uint8_t note_number);
  [[nodiscard]] bool handle_channel_selection(uint8_t note_number);
  [[nodiscard]] bool handle_layer_selection(uint8_t note_number);
  [[nodiscard]] bool handle_dial_mode_selection(uint8_t note_number);
  [[nodiscard]] bool handle_filter_params_increment(uint8_t note_number);

  void handle_normalized_control_change(uint8_t index, double value) const;
  [[nodiscard]] bool handle_volume_control_change(uint8_t index,
                                                  double value) const;
  [[nodiscard]] bool handle_send_volume_control_change(uint8_t index,
                                                       double value) const;
  [[nodiscard]] bool handle_filter_params_control_change(uint8_t index,
                                                         double value) const;
  [[nodiscard]] bool handle_master_volume_control_change(uint8_t index,
                                                         double value) const;

  void add_channel_feedback() const;
  void add_layer_feedback() const;
  void add_dial_mode_feedback() const;

  void call_channel_callback() const;
  void call_layer_callback() const;
  void call_dial_mode_callback(size_t channel_id, const Channel &channel) const;
  void call_filter_params_section_select_callback(size_t channel_id,
                                                  const Channel &channel) const;

  [[nodiscard]] size_t layer_channel_offset() const {
    return _selected_layer_id * _channels_per_layer;
  }

  static std::optional<DialColumnAndRow>
  get_column_and_row_for_dial_index(uint8_t index);
};

} // namespace controllers
