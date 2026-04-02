#pragma once

#include "Callbacks.h"
#include "DialColumnAndRow.h"
#include "IController.h"
#include "controllers/Channel.h"
#include "midi/Messages.h"
#include "midi/Sender.h"
#include "registry/Registry.h"

#include <functional>
#include <utility>

namespace controllers {

class MidiMix : public IController {
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

  void set_layer_from_processor(size_t layer) override;
  void set_selected_channel_from_processor(ChannelType channel_type,
                                           size_t channel_id) override;

  void clear_selected_channel_from_processor() override;

  void set_selected_plugin_page_from_processor(size_t plugin_id,
                                               size_t page_number) override;

  void set_channel_playback_node(size_t channel_id, uint64_t object_id);
  void set_channel_filter_node(size_t channel_id, uint64_t object_id);

  void add_current_state_as_feedback() override;

  void set_master_channel_node(size_t layer_id, uint64_t object_id) override;
  void set_channel_node(ChannelType channel_type, size_t channel_id,
                        uint64_t object_id) override;
  void set_channel_filter_node(ChannelType channel_type, size_t channel_id,
                               uint64_t object_id) override;
  void set_channel_send_node(ChannelType channel_type, size_t channel_id,
                             size_t send_id, uint64_t object_id) override;
  void clear_filter_parameters(ChannelType channel_type,
                               size_t channel_id) override;
  void add_filter_parameter(ChannelType channel_type, size_t channel_id,
                            size_t plugin_id, char *name, float min,
                            float max) override;

private:
  static constexpr size_t _number_of_layers{2};
  static constexpr size_t _channels_per_layer{8};

  registry::Registry &_registry;
  pw_main_loop *_loop;
  midi::SenderPointer _feedback_channel;

  std::atomic<size_t> _selected_layer_id{0};
  std::atomic<std::optional<size_t>> _selected_channel_id{std::nullopt};

  std::array<registry::Node *, 2> _master_channel_playback_nodes = {nullptr,
                                                                    nullptr};

  std::mutex _channels_mutex{};
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

  void handle_normalized_control_change(uint8_t index, double value);
  [[nodiscard]] bool handle_volume_control_change(uint8_t index, double value);
  [[nodiscard]] bool handle_send_volume_control_change(uint8_t index,
                                                       double value);
  [[nodiscard]] bool handle_filter_params_control_change(uint8_t index,
                                                         double value);
  [[nodiscard]] bool handle_master_volume_control_change(uint8_t index,
                                                         double value) const;

  void add_channel_feedback() const;
  void add_layer_feedback() const;
  void add_dial_mode_feedback();

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

  std::optional<std::reference_wrapper<Channel>>
  channel_by_type_and_id(ChannelType channel_type, size_t channel_id);
};

} // namespace controllers
