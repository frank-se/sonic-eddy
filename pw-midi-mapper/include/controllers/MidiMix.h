#pragma once

#include "Callbacks.h"
#include "action_container/IActionContainer.h"
#include "controllers/Channel.h"
#include "midi/Messages.h"
#include "midi/Sender.h"
#include "pw_utils/SetParamsData.h"

#include <functional>
#include <utility>

namespace controllers {

class MidiMix : public action_container::IActionContainer {
public:
  MidiMix(
      pw_registry *registry, pw_main_loop *loop,
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
  void bind_channel_playback_node(size_t channel_id);

  void set_channel_filter_playback_node(size_t channel_id, uint64_t object_id);

  static void
  on_channel_playback_node_params_changed(void *user_data, int sequence_number,
                                          uint32_t id, uint32_t index,
                                          uint32_t next, const spa_pod *pod);

  using LayerSelectCallback = std::function<void(size_t layer_id)>;

  using ChannelSelectCallback = std::function<void(size_t channel_id)>;

  using DialSectionModeSelectCallback =
      std::function<void(size_t channel_id, DialMode)>;

  using FilterParamsSectionSelectCallback =
      std::function<void(size_t channel_id, size_t section_id)>;

private:
  pw_registry *_registry;
  pw_main_loop *_loop;
  midi::SenderPointer _feedback_channel;

  std::atomic<size_t> _selected_layer_id{0};
  std::atomic<std::optional<size_t>> _selected_channel_id{std::nullopt};

  void handle_note_on(uint8_t note_number);
  bool handle_channel_selection(uint8_t note_number);
  bool handle_layer_selection(uint8_t note_number);
  bool handle_dial_mode_selection(uint8_t note_number);
  bool handle_filter_params_increment(uint8_t note_number);

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

  NodeData master_channel_playback_node;
  std::atomic<float> master_volume;
  std::atomic<float> master_pan;

  void call_channel_callback() const;
  void call_layer_callback() const;
  void call_dial_mode_callback(size_t channel_id, const Channel &channel) const;
  void call_filter_params_section_select_callback(size_t channel_id,
                                                  const Channel &channel) const;

  LayerSelectCallback _layer_select_callback;
  ChannelSelectCallback _channel_select_callback;
  DialSectionModeSelectCallback _dial_section_mode_select_callback;
  FilterParamsSectionSelectCallback _filter_params_section_select_callback;

  /*
   * We always have 16 channels, each assigned to a layer. The first 8 are
   * assigned to layer 0, and the second 8 are assigned to layer 1.
   */
  std::array<Channel, 16> _channels{};

  struct DialChannelAndRow {
    uint8_t controller_channel_id;
    uint8_t dial_row;
  };

  static std::optional<DialChannelAndRow>
  get_channel_id_for_dial_index(uint8_t index);
};
} // namespace controllers
