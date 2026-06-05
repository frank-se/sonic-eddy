#pragma once

#include "Callbacks.h"
#include "IController.h"
#include "midi/Sender.h"

#include <array>
#include <mutex>
#include <optional>
#include <utility>
#include <vector>

namespace controllers {

enum class LaunchpadMiniMode { Channel, GroupAndMaster };
enum class LaunchpadMiniLayout { Session, Fader };

using LaunchpadMiniLoopPressedCallback =
    std::function<void(size_t layer_id, ChannelType channel_type,
                       size_t channel_id, size_t loop_position)>;

using LaunchpadMiniFaderChangedCallback =
    std::function<void(size_t layer_id, ChannelType channel_type,
                       size_t channel_id, float normalized_value)>;

class LaunchpadMini : public IController {
public:
  LaunchpadMini(midi::SenderPointer mi_sender, midi::SenderPointer da_sender,
                LayerSelectCallback layer_select_callback,
                LaunchpadMiniLoopPressedCallback loop_pressed_callback,
                LaunchpadMiniFaderChangedCallback fader_changed_callback);

  void process(const midi::Message &message) override;

  void set_layer_from_processor(size_t layer) override;
  void set_selected_channel_from_processor(ChannelType channel_type,
                                           size_t channel_id) override {}
  void clear_selected_channel_from_processor() override {}
  void set_selected_plugin_page_from_processor(size_t plugin_id,
                                               size_t page_number) override {}
  void add_current_state_as_feedback() override;

  void set_master_channel_node(size_t layer_id, uint64_t object_id) override {}
  void set_channel_node(ChannelType channel_type, size_t channel_id,
                        uint64_t object_id) override {}
  void set_channel_filter_node(ChannelType channel_type, size_t channel_id,
                               uint64_t object_id) override {}
  void set_channel_send_node(ChannelType channel_type, size_t channel_id,
                             size_t send_id, uint64_t object_id) override {}
  void clear_filter_parameters(ChannelType channel_type,
                               size_t channel_id) override {}
  void add_filter_parameter(ChannelType channel_type, size_t channel_id,
                            size_t plugin_id, char *name, float min,
                            float max) override {}

  void set_looper_slot_state(ChannelType channel_type, size_t channel_id,
                             size_t loop_position, bool available, bool loaded,
                             bool playing);

private:
  struct SlotState {
    bool available{};
    bool loaded{};
    bool playing{};
  };

  struct Target {
    ChannelType channel_type{};
    size_t channel_id{};
    bool available{};
  };

  static constexpr size_t _layer_count = 2;
  static constexpr size_t _channels_per_layer = 8;
  static constexpr size_t _group_channels_per_layer = 4;
  static constexpr size_t _loop_positions = 8;
  static constexpr uint8_t _static_led_channel = 0;
  static constexpr uint8_t _flashing_led_channel = 1;

  static constexpr uint8_t _layer_a_cc = 89;
  static constexpr uint8_t _layer_b_cc = 79;
  static constexpr uint8_t _channel_mode_cc = 69;
  static constexpr uint8_t _group_master_mode_cc = 59;
  static constexpr uint8_t _fader_layout_cc = 19;

  static constexpr uint8_t _off = 0;
  static constexpr uint8_t _dark_green = 21;
  static constexpr uint8_t _bright_green = 87;
  static constexpr uint8_t _dark_blue = 41;
  static constexpr uint8_t _bright_blue = 45;
  static constexpr uint8_t _dark_yellow = 62;
  static constexpr uint8_t _bright_yellow = 13;

  midi::SenderPointer _mi_sender;
  midi::SenderPointer _da_sender;
  LayerSelectCallback _layer_select_callback;
  LaunchpadMiniLoopPressedCallback _loop_pressed_callback;
  LaunchpadMiniFaderChangedCallback _fader_changed_callback;

  std::mutex _state_mutex;
  size_t _selected_layer{};
  LaunchpadMiniMode _mode{LaunchpadMiniMode::Channel};
  LaunchpadMiniLayout _layout{LaunchpadMiniLayout::Session};
  bool _initialized{};

  std::array<std::array<SlotState, _loop_positions>,
             _layer_count * (_channels_per_layer + _group_channels_per_layer) +
                 _layer_count>
      _slot_states{};

  void initialize();
  void handle_raw_ump(const midi::RawUmpMessage &message);
  void handle_note_on(uint8_t note_number, uint16_t velocity);
  void handle_control_change(uint8_t index, double value);

  void switch_layout(LaunchpadMiniLayout layout);
  void switch_mode(LaunchpadMiniMode mode);

  void send_sysex7(const std::vector<uint8_t> &payload) const;
  void send_raw(std::initializer_list<uint32_t> words,
                const midi::SenderPointer &sender) const;
  void send_note(uint8_t note, uint8_t velocity,
                 uint8_t channel = _static_led_channel) const;
  void send_cc(uint8_t index, uint8_t value) const;
  void send_layout(LaunchpadMiniLayout layout) const;
  void send_fader_setup() const;

  void refresh_all();
  void refresh_layer_buttons();
  void refresh_mode_buttons();
  void refresh_layout_button();
  void refresh_grid();

  [[nodiscard]] std::optional<Target> target_for_column(size_t column) const;
  [[nodiscard]] std::optional<std::pair<size_t, size_t>>
  grid_position_for_note(uint8_t note_number) const;
  [[nodiscard]] std::optional<size_t> state_index(ChannelType channel_type,
                                                  size_t channel_id) const;
};

} // namespace controllers
