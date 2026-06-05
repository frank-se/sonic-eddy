#include "controllers/LaunchpadMini.h"

#include "logging/log.h"

#include <algorithm>

controllers::LaunchpadMini::LaunchpadMini(
    midi::SenderPointer mi_sender, midi::SenderPointer da_sender,
    LayerSelectCallback layer_select_callback,
    LaunchpadMiniLoopPressedCallback loop_pressed_callback,
    LaunchpadMiniFaderChangedCallback fader_changed_callback)
    : _mi_sender(std::move(mi_sender)), _da_sender(std::move(da_sender)),
      _layer_select_callback(std::move(layer_select_callback)),
      _loop_pressed_callback(std::move(loop_pressed_callback)),
      _fader_changed_callback(std::move(fader_changed_callback)) {
  initialize();
}

void controllers::LaunchpadMini::initialize() {
  {
    std::lock_guard lock(_state_mutex);
    if (_initialized)
      return;
    _initialized = true;
  }

  send_raw({0x30047e7f, 0x06010000}, _mi_sender);
  send_raw({0x30160020, 0x29020d10}, _mi_sender);
  send_raw({0x30310100, 0x00000000}, _mi_sender);
  send_raw({0x20815190}, _mi_sender);

  refresh_all();
}

void controllers::LaunchpadMini::process(const midi::Message &message) {
  std::visit(
      [this](auto &&arg) {
        using T = std::decay_t<decltype(arg)>;
        if constexpr (std::is_same_v<T, midi::RawUmpMessage>) {
          handle_raw_ump(arg);
        } else if constexpr (std::is_same_v<T, midi::NoteOnV1>) {
          handle_note_on(arg.note_number, arg.velocity);
        } else if constexpr (std::is_same_v<T, midi::NoteOnV2>) {
          handle_note_on(arg.note_number, arg.velocity);
        } else if constexpr (std::is_same_v<T, midi::ControlChangeV1>) {
          handle_control_change(arg.index, midi::normalized_cc_value(arg));
        } else if constexpr (std::is_same_v<T, midi::ControlChangeV2>) {
          handle_control_change(arg.index, midi::normalized_cc_value(arg));
        }
      },
      message);
}

void controllers::LaunchpadMini::handle_raw_ump(
    const midi::RawUmpMessage &message) {
  if (message.word_count < 2)
    return;

  if (message.words[0] == 806780416u || message.words[0] == 807807251u ||
      message.words[0] == 808649734u) {
    logging::log<logging::LogLevel::Debug>(
        "Launchpad Mini init handshake word {}", message.words[0]);
  }
}

void controllers::LaunchpadMini::handle_note_on(const uint8_t note_number,
                                                const uint16_t velocity) {
  if (velocity == 0)
    return;

  std::optional<Target> target;
  size_t loop_position{};
  {
    std::lock_guard lock(_state_mutex);
    const auto grid_position = grid_position_for_note(note_number);
    if (!grid_position)
      return;

    target = target_for_column(grid_position->first);
    loop_position = grid_position->second;
  }

  if (!target || !target->available)
    return;

  _loop_pressed_callback(_selected_layer, target->channel_type,
                         target->channel_id, loop_position);
}

void controllers::LaunchpadMini::handle_control_change(const uint8_t index,
                                                       const double value) {
  if (index == _layer_a_cc) {
    set_layer_from_processor(0);
    _layer_select_callback(0);
    return;
  }

  if (index == _layer_b_cc) {
    set_layer_from_processor(1);
    _layer_select_callback(1);
    return;
  }

  if (index == _channel_mode_cc) {
    switch_mode(LaunchpadMiniMode::Channel);
    return;
  }

  if (index == _group_master_mode_cc) {
    switch_mode(LaunchpadMiniMode::GroupAndMaster);
    return;
  }

  if (index == _fader_layout_cc) {
    switch_layout(_layout == LaunchpadMiniLayout::Fader
                      ? LaunchpadMiniLayout::Session
                      : LaunchpadMiniLayout::Fader);
    return;
  }

  if (index <= 7) {
    std::optional<Target> target;
    {
      std::lock_guard lock(_state_mutex);
      if (_layout != LaunchpadMiniLayout::Fader)
        return;
      target = target_for_column(index);
    }

    if (target && target->available)
      _fader_changed_callback(_selected_layer, target->channel_type,
                              target->channel_id, static_cast<float>(value));
  }
}

void controllers::LaunchpadMini::set_layer_from_processor(const size_t layer) {
  if (layer >= _layer_count)
    return;

  {
    std::lock_guard lock(_state_mutex);
    if (_selected_layer == layer)
      return;

    _selected_layer = layer;
  }
  refresh_all();
}

void controllers::LaunchpadMini::switch_mode(const LaunchpadMiniMode mode) {
  {
    std::lock_guard lock(_state_mutex);
    if (_mode == mode)
      return;

    _mode = mode;
  }
  refresh_all();
}

void controllers::LaunchpadMini::switch_layout(
    const LaunchpadMiniLayout layout) {
  {
    std::lock_guard lock(_state_mutex);
    if (_layout == layout)
      return;

    _layout = layout;
  }

  send_layout(layout);
  if (layout == LaunchpadMiniLayout::Fader)
    send_fader_setup();
  refresh_all();
}

void controllers::LaunchpadMini::add_current_state_as_feedback() {
  refresh_all();
}

void controllers::LaunchpadMini::set_looper_slot_state(
    const ChannelType channel_type, const size_t channel_id,
    const size_t loop_position, const bool available, const bool loaded,
    const bool playing) {
  if (loop_position >= _loop_positions)
    return;

  {
    std::lock_guard lock(_state_mutex);
    const auto index = state_index(channel_type, channel_id);
    if (!index) {
      logging::log<logging::LogLevel::Error>(
          "Launchpad Mini looper state target out of range: channel type {}, "
          "channel id {}",
          static_cast<int>(channel_type), channel_id);
      return;
    }

    _slot_states[*index][loop_position] =
        SlotState{.available = available, .loaded = loaded, .playing = playing};
  }

  refresh_grid();
}

void controllers::LaunchpadMini::send_raw(
    const std::initializer_list<uint32_t> words,
    const midi::SenderPointer &sender) const {
  midi::RawUmpMessage message{};
  message.word_count = static_cast<uint8_t>(words.size());
  std::ranges::copy(words, message.words.begin());
  sender->push(message);
}

void controllers::LaunchpadMini::send_sysex7(
    const std::vector<uint8_t> &payload) const {
  size_t offset = 0;
  while (offset < payload.size()) {
    const auto remaining = payload.size() - offset;
    const auto count = static_cast<uint8_t>(std::min<size_t>(remaining, 6));
    const bool first = offset == 0;
    const bool last = remaining <= 6;
    const uint8_t status = payload.size() <= 6 ? 0 : first ? 1 : last ? 3 : 2;

    std::array<uint8_t, 8> bytes{};
    bytes[0] = 0x30;
    bytes[1] = static_cast<uint8_t>((status << 4) | count);
    for (size_t i = 0; i < count; i++)
      bytes[i + 2] = payload[offset + i];

    const auto word0 = static_cast<uint32_t>(bytes[0]) << 24 |
                       static_cast<uint32_t>(bytes[1]) << 16 |
                       static_cast<uint32_t>(bytes[2]) << 8 | bytes[3];
    const auto word1 = static_cast<uint32_t>(bytes[4]) << 24 |
                       static_cast<uint32_t>(bytes[5]) << 16 |
                       static_cast<uint32_t>(bytes[6]) << 8 | bytes[7];

    send_raw({word0, word1}, _da_sender);
    offset += count;
  }
}

void controllers::LaunchpadMini::send_note(const uint8_t note,
                                           const uint8_t velocity,
                                           const uint8_t channel) const {
  _da_sender->push(midi::NoteOnV1{
      .channel = channel,
      .note_number = note,
      .velocity = velocity,
  });
}

void controllers::LaunchpadMini::send_cc(const uint8_t index,
                                         const uint8_t value) const {
  _da_sender->push(midi::ControlChangeV1{
      .channel = _static_led_channel,
      .index = index,
      .value = value,
  });
}

void controllers::LaunchpadMini::send_layout(
    const LaunchpadMiniLayout layout) const {
  send_sysex7({0x00, 0x20, 0x29, 0x02, 0x0d, 0x00,
               static_cast<uint8_t>(layout == LaunchpadMiniLayout::Session
                                        ? 0x00
                                        : 0x0d)});
}

void controllers::LaunchpadMini::send_fader_setup() const {
  send_sysex7({
      0x00, 0x20, 0x29, 0x02, 0x0d, 0x01, 0x00, 0x00,
      0x00, 0x01, 0x00, 0x15, 0x01, 0x01, 0x01, 0x15,
      0x02, 0x01, 0x02, 0x15, 0x03, 0x01, 0x03, 0x15,
      0x04, 0x01, 0x04, 0x15, 0x05, 0x01, 0x05, 0x15,
      0x06, 0x01, 0x06, 0x15, 0x07, 0x01, 0x07, 0x15,
  });
}

void controllers::LaunchpadMini::refresh_all() {
  refresh_layer_buttons();
  refresh_mode_buttons();
  refresh_layout_button();
  refresh_grid();
}

void controllers::LaunchpadMini::refresh_layer_buttons() {
  send_cc(_layer_a_cc, _selected_layer == 0 ? _bright_green : _dark_green);
  send_cc(_layer_b_cc, _selected_layer == 1 ? _bright_green : _dark_green);
}

void controllers::LaunchpadMini::refresh_mode_buttons() {
  send_cc(_channel_mode_cc,
          _mode == LaunchpadMiniMode::Channel ? _bright_blue : _dark_blue);
  send_cc(_group_master_mode_cc,
          _mode == LaunchpadMiniMode::GroupAndMaster ? _bright_blue
                                                     : _dark_blue);
}

void controllers::LaunchpadMini::refresh_layout_button() {
  send_cc(_fader_layout_cc,
          _layout == LaunchpadMiniLayout::Fader ? _bright_blue : _dark_blue);
}

void controllers::LaunchpadMini::refresh_grid() {
  std::lock_guard lock(_state_mutex);
  for (size_t row = 0; row < _loop_positions; row++) {
    for (size_t column = 0; column < _channels_per_layer; column++) {
      const auto note = static_cast<uint8_t>((8 - row) * 10 + column + 1);
      const auto target = target_for_column(column);
      if (!target || !target->available) {
        send_note(note, _off);
        send_note(note, _off, _flashing_led_channel);
        continue;
      }

      const auto index = state_index(target->channel_type, target->channel_id);
      if (!index) {
        send_note(note, _off);
        send_note(note, _off, _flashing_led_channel);
        continue;
      }

      const auto state = _slot_states[*index][row];
      if (!state.available) {
        send_note(note, _off);
        send_note(note, _off, _flashing_led_channel);
      } else if (state.playing) {
        send_note(note, _off);
        send_note(note, _bright_yellow, _flashing_led_channel);
      } else if (state.loaded) {
        send_note(note, _off, _flashing_led_channel);
        send_note(note, _bright_yellow);
      } else {
        send_note(note, _off, _flashing_led_channel);
        send_note(note, _dark_yellow);
      }
    }
  }
}

std::optional<controllers::LaunchpadMini::Target>
controllers::LaunchpadMini::target_for_column(const size_t column) const {
  if (column >= _channels_per_layer)
    return std::nullopt;

  if (_mode == LaunchpadMiniMode::Channel) {
    return Target{.channel_type = ChannelType::CHANNEL,
                  .channel_id = _selected_layer * _channels_per_layer + column,
                  .available = true};
  }

  if (column < _group_channels_per_layer) {
    return Target{
        .channel_type = ChannelType::GROUP_CHANNEL,
        .channel_id = _selected_layer * _group_channels_per_layer + column,
        .available = true};
  }

  if (column == _group_channels_per_layer) {
    return Target{.channel_type = ChannelType::MASTER,
                  .channel_id = _selected_layer,
                  .available = true};
  }

  return Target{.available = false};
}

std::optional<std::pair<size_t, size_t>>
controllers::LaunchpadMini::grid_position_for_note(
    const uint8_t note_number) const {
  const auto ones = note_number % 10;
  if (ones < 1 || ones > 8)
    return std::nullopt;

  const auto tens = note_number / 10;
  if (tens < 1 || tens > 8)
    return std::nullopt;

  return std::pair<size_t, size_t>{ones - 1, 8 - tens};
}

std::optional<size_t> controllers::LaunchpadMini::state_index(
    const ChannelType channel_type, const size_t channel_id) const {
  if (channel_type == ChannelType::CHANNEL) {
    if (channel_id >= _layer_count * _channels_per_layer)
      return std::nullopt;
    return channel_id;
  }

  if (channel_type == ChannelType::GROUP_CHANNEL) {
    if (channel_id >= _layer_count * _group_channels_per_layer)
      return std::nullopt;
    return _layer_count * _channels_per_layer + channel_id;
  }

  if (channel_type != ChannelType::MASTER || channel_id >= _layer_count)
    return std::nullopt;

  return _layer_count * (_channels_per_layer + _group_channels_per_layer) +
         channel_id;
}
