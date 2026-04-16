#include <controllers/CmdMm1.h>

#include "logging/log.h"

#include <iostream>

void controllers::CmdMm1::process(const midi::Message &message) {
  logging::log<logging::LogLevel::Trace>("CmdMm1::process");

  std::visit(
      [this](auto &&arg) {
        using T = std::decay_t<decltype(arg)>;
        if constexpr (std::is_same_v<T, midi::NoteOnV1>) {
          handle_note_on(arg.note_number);
        } else if constexpr (std::is_same_v<T, midi::NoteOnV2>) {
          handle_note_on(arg.note_number);
        } else if constexpr (std::is_same_v<T, midi::ControlChangeV1>) {
          handle_normalized_control_change(arg.index,
                                           midi::normalized_cc_value(arg));
        } else if constexpr (std::is_same_v<T, midi::ControlChangeV2>) {
          handle_normalized_control_change(arg.index,
                                           midi::normalized_cc_value(arg));
        }
      },
      message);
}

void controllers::CmdMm1::handle_note_on(const uint8_t note_number) {
  logging::log<logging::LogLevel::Trace>("CmdMm1::handle_note_on");

  if (handle_channel_selection(note_number)) {
    call_channel_callback();
    add_channel_feedback();
  } else if (handle_layer_selection(note_number)) {
    call_layer_callback();
    add_layer_feedback();
    add_channel_feedback();
    add_dial_mode_feedback();
    add_filter_params_feedback();
  } else if (handle_dial_mode_selection(note_number)) {
    add_dial_mode_feedback();
  } else if (handle_filter_params_increment(note_number)) {
    add_filter_params_feedback();
  } else if (handle_filter_params_page_select(note_number)) {
  }
}

bool controllers::CmdMm1::handle_channel_selection(const uint8_t note_number) {
  logging::log<logging::LogLevel::Trace>("CmdMm1::handle_channel_selection");

  bool handled = false;
  size_t new_channel_id{0};

  if (note_number == 48) {
    new_channel_id = 0 + layer_channel_offset();
    handled = true;
  } else if (note_number == 49) {
    new_channel_id = 1 + layer_channel_offset();
    handled = true;
  } else if (note_number == 50) {
    new_channel_id = 2 + layer_channel_offset();
    handled = true;
  } else if (note_number == 51) {
    new_channel_id = 3 + layer_channel_offset();
    handled = true;
  }

  if (handled) {
    logging::log<logging::LogLevel::Debug>("Setting active channel to {}",
                                           new_channel_id);

    _selected_channel_id = new_channel_id;
    return true;
  }

  logging::log<logging::LogLevel::Debug>(
      "Note number {} not used for channel selection", note_number);

  return false;
}

bool controllers::CmdMm1::handle_layer_selection(const uint8_t note_number) {
  logging::log<logging::LogLevel::Trace>("CmdMm1::handle_layer_selection");

  if (note_number == 18) {
    _selected_layer_id = (_selected_layer_id + 1) & 1;

    logging::log<logging::LogLevel::Debug>("Set active layer to {}",
                                           _selected_layer_id.load());

    return true;
  }

  logging::log<logging::LogLevel::Debug>(
      "Note number {} not used for layer changes", note_number);

  return false;
}

bool controllers::CmdMm1::handle_dial_mode_selection(
    const uint8_t note_number) {
  logging::log<logging::LogLevel::Trace>("CmdMm1::handle_dial_mode_selection");

  size_t channel_id{0};

  bool valid_id{false};

  if (note_number == 19) {
    channel_id = 0;
    valid_id = true;
  } else if (note_number == 23) {
    channel_id = 1;
    valid_id = true;
  } else if (note_number == 27) {
    channel_id = 2;
    valid_id = true;
  } else if (note_number == 31) {
    channel_id = 3;
    valid_id = true;
  }

  if (!valid_id) {
    logging::log<logging::LogLevel::Debug>(
        "Note number {} not used for dial mode selection", note_number);

    return false;
  }

  channel_id += layer_channel_offset();

  std::lock_guard lock(_channels_mutex);
  auto &channel = _channels[channel_id];

  channel.swap_dial_mode();

  call_dial_mode_callback(channel_id, channel);

  return true;
}

bool controllers::CmdMm1::handle_filter_params_increment(
    const uint8_t note_number) {
  logging::log<logging::LogLevel::Trace>(
      "MidiMix::handle_filter_params_increment");

  size_t channel_id{0};
  bool valid_id{false};
  if (note_number == 20) {
    channel_id = 0;
    valid_id = true;
  } else if (note_number == 24) {
    channel_id = 1;
    valid_id = true;
  } else if (note_number == 28) {
    channel_id = 2;
    valid_id = true;
  } else if (note_number == 32) {
    channel_id = 3;
    valid_id = true;
  }

  if (!valid_id) {
    logging::log<logging::LogLevel::Debug>(
        "Note number {} not used for filter params target increment",
        note_number);

    return false;
  }

  channel_id += layer_channel_offset();

  std::lock_guard lock(_channels_mutex);
  auto &channel = _channels[channel_id];

  channel.increment_selected_filter_param_section();

  call_filter_params_section_select_callback(channel_id, channel);

  return true;
}

bool controllers::CmdMm1::handle_filter_params_page_select(
    const uint8_t note_number) {
  logging::log<logging::LogLevel::Trace>(
      "CmdMm1::handle_filter_params_page_select");

  if (note_number == 16) {
    logging::log<logging::LogLevel::Debug>(
        "Decrementing selected filter params page");

    _filter_params_pages_left_callback(1);

    return true;
  } else if (note_number == 17) {
    logging::log<logging::LogLevel::Debug>(
        "Incrementing selected filter params page");

    _filter_params_pages_right_callback(1);

    return true;
  }

  return false;
}

void controllers::CmdMm1::handle_normalized_control_change(const uint8_t index,
                                                           const double value) {
  logging::log<logging::LogLevel::Trace>(
      "CmdMm1::handle_normalized_control_change");

  if (handle_volume_control_change(index, value))
    return;

  if (handle_send_volume_control_change(index, value))
    return;

  if (handle_filter_params_control_change(index, value))
    return;
}

bool controllers::CmdMm1::handle_volume_control_change(const uint8_t index,
                                                       const double value) {
  logging::log<logging::LogLevel::Trace>(
      "CmdMm1::handle_volume_control_change");

  bool valid_channel = false;
  size_t channel_id{0};
  if (index == 48) {
    valid_channel = true;
    channel_id = 0 + layer_channel_offset();
  } else if (index == 49) {
    valid_channel = true;
    channel_id = 1 + layer_channel_offset();
  } else if (index == 50) {
    valid_channel = true;
    channel_id = 2 + layer_channel_offset();
  } else if (index == 51) {
    valid_channel = true;
    channel_id = 3 + layer_channel_offset();
  }

  if (!valid_channel) {
    logging::log<logging::LogLevel::Debug>(
        "Control index {} not used for volume control", index);

    return false;
  }

  logging::log<logging::LogLevel::Debug>(
      "Processing volume change for channel {}", channel_id);

  std::lock_guard lock(_channels_mutex);
  const auto &channel = _channels[channel_id];

  channel.set_volume(value);

  return true;
}

bool controllers::CmdMm1::handle_send_volume_control_change(
    const uint8_t index, const double value) {
  logging::log<logging::LogLevel::Trace>(
      "CmdMm1::handle_send_volume_control_change");

  const auto column_and_row = get_column_and_row_for_dial_index(index);

  if (!column_and_row) {
    logging::log<logging::LogLevel::Debug>("Control index {} is not a dial",
                                           index);

    return false;
  }

  std::lock_guard lock(_channels_mutex);
  const auto &channel =
      _channels[column_and_row->column + layer_channel_offset()];

  if (channel.dial_mode() == FILTER_PARAMS) {
    logging::log<logging::LogLevel::Debug>(
        "Dials mode for channel {} is set to FILTER_PARAMS, ignoring",
        column_and_row->column);

    return false;
  }

  logging::log<logging::LogLevel::Debug>("Setting send volume {}", value);

  channel.set_send_trim(column_and_row->row, value);

  return true;
}

bool controllers::CmdMm1::handle_filter_params_control_change(
    const uint8_t index, const double value) {
  logging::log<logging::LogLevel::Trace>(
      "CmdMm1::handle_filter_params_control_change");

  const auto column_and_row = get_column_and_row_for_dial_index(index);

  if (!column_and_row) {
    logging::log<logging::LogLevel::Debug>("Control index {} is not a dial",
                                           index);

    return false;
  }

  std::lock_guard lock(_channels_mutex);
  const auto &channel =
      _channels[column_and_row->column + layer_channel_offset()];

  if (channel.dial_mode() == SENDS) {
    logging::log<logging::LogLevel::Debug>(
        "Dials mode for channel {} is set to SENDS, ignoring",
        column_and_row->column);

    return false;
  }

  const auto row = column_and_row->row;

  channel.set_parameter_for_selected_section(row, value);

  return true;
}

void controllers::CmdMm1::add_channel_feedback() const {
  logging::log<logging::LogLevel::Trace>("CmdMm1::add_channel_feedback");

  const auto channel = _selected_channel_id.load();

  if (!channel) {
    logging::log<logging::LogLevel::Debug>(
        "No active channel id, turning all rec arm lights off");

    for (size_t i = 0; i < _channels_per_layer; i++) {
      auto note_number = static_cast<uint8_t>(48 + i);
      logging::log<logging::LogLevel::Debug>(
          "Adding note on for note number {} with velocity {}", note_number,
          _button_feedback_off);

      _feedback_channel->push(midi::NoteOnV1{
          .channel = _midi_channel,
          .note_number = note_number,
          .velocity = _button_feedback_off,
      });
    }
  } else if ((_selected_layer_id == 0 && *channel < _channels_per_layer) ||
             (_selected_layer_id == 1 && *channel >= _channels_per_layer &&
              channel < 2 * _channels_per_layer)) {
    const auto highlighted_controller_channel =
        _selected_layer_id == 0 ? *channel : *channel - _channels_per_layer;

    logging::log<logging::LogLevel::Debug>(
        "Selected channel visible, turning on selected channel rec arm light "
        "for channel id {}",
        highlighted_controller_channel);

    for (size_t i = 0; i < _channels_per_layer; i++) {
      const auto note_number = static_cast<uint8_t>(48 + i);
      const auto velocity = static_cast<uint8_t>(
          i == highlighted_controller_channel ? _button_feedback_on_solid
                                              : _button_feedback_off);

      logging::log<logging::LogLevel::Debug>(
          "Adding note on for note number {} with velocity {}", note_number,
          velocity);

      _feedback_channel->push(midi::NoteOnV1{
          .channel = _midi_channel,
          .note_number = note_number,
          .velocity = velocity,
      });
    }
  } else {
    logging::log<logging::LogLevel::Debug>(
        "Selected channel not visible, turning all rec arm lights off");

    for (size_t i = 0; i < _channels_per_layer; i++) {
      auto note_number = static_cast<uint8_t>(48 + i);

      logging::log<logging::LogLevel::Debug>(
          "Adding note on for note number {} with velocity 0", note_number);

      _feedback_channel->push(midi::NoteOnV1{
          .channel = _midi_channel,
          .note_number = note_number,
          .velocity = _button_feedback_off,
      });
    }
  }
}

void controllers::CmdMm1::add_layer_feedback() const {
  logging::log<logging::LogLevel::Trace>("CmdMm1::add_layer_feedback");

  const auto layer = _selected_layer_id.load();

  _feedback_channel->push(midi::NoteOnV1{
      .channel = _midi_channel,
      .note_number = 18,
      .velocity = static_cast<uint8_t>(layer == 0 ? _button_feedback_off
                                                  : _button_feedback_on_solid),
  });
}

void controllers::CmdMm1::add_dial_mode_feedback() {
  logging::log<logging::LogLevel::Trace>("CmdMm1::add_dial_mode_feedback");

  std::lock_guard lock(_channels_mutex);
  for (size_t i = 0; i < _channels_per_layer; i++) {
    const auto channel_id = i + layer_channel_offset();
    auto &channel = _channels[channel_id];

    auto note_number = static_cast<uint8_t>(19 + 4 * i);

    auto velocity = channel.dial_mode() == FILTER_PARAMS
                        ? _button_feedback_on_solid
                        : _button_feedback_off;

    logging::log<logging::LogLevel::Debug>(
        "Adding note on for note number {} with velocity {}", note_number,
        velocity);

    _feedback_channel->push(midi::NoteOnV1{
        .channel = _midi_channel,
        .note_number = note_number,
        .velocity = velocity,
    });
  }
}

void controllers::CmdMm1::add_filter_params_feedback() {
  logging::log<logging::LogLevel::Trace>("CmdMm1::add_filter_params_feedback");

  std::lock_guard lock(_channels_mutex);
  for (size_t i = 0; i < _channels_per_layer; i++) {
    const auto channel_id = i + layer_channel_offset();
    auto &channel = _channels[channel_id];

    auto note_number = static_cast<uint8_t>(20 + 4 * i);

    auto velocity = channel.selected_filter_params_section();

    logging::log<logging::LogLevel::Debug>(
        "Setting note number {} with velocity {} for channel {}", note_number,
        velocity, channel_id);

    _feedback_channel->push(midi::NoteOnV1{
        .channel = _midi_channel,
        .note_number = note_number,
        .velocity = velocity,
    });
  }
}

void controllers::CmdMm1::call_channel_callback() const {
  logging::log<logging::LogLevel::Trace>("CmdMm1::call_channel_callback");

  if (const auto channel_id = _selected_channel_id.load())
    _channel_select_callback(*channel_id);
}

void controllers::CmdMm1::call_layer_callback() const {
  logging::log<logging::LogLevel::Trace>("CmdMm1::call_layer_callback");

  const auto id = _selected_layer_id.load();
  _layer_select_callback(id);
}

void controllers::CmdMm1::call_dial_mode_callback(
    size_t channel_id, const Channel &channel) const {
  logging::log<logging::LogLevel::Trace>("CmdMm1::call_dial_mode_callback");

  _dial_section_mode_callback(channel_id, channel.dial_mode());
}

void controllers::CmdMm1::call_filter_params_section_select_callback(
    size_t channel_id, const Channel &channel) const {
  logging::log<logging::LogLevel::Trace>(
      "CmdMm1::call_filter_params_section_select_callback");

  _filter_params_section_select_callback(
      channel_id, channel.selected_filter_params_section());
}

std::optional<controllers::DialColumnAndRow>
controllers::CmdMm1::get_column_and_row_for_dial_index(uint8_t index) {
  if (index >= 6 && index <= 9) {
    return DialColumnAndRow{.column = static_cast<uint8_t>(index - 6),
                            .row = 0};
  }

  if (index >= 10 && index <= 13) {
    return DialColumnAndRow{.column = static_cast<uint8_t>(index - 10),
                            .row = 1};
  }

  if (index >= 14 && index <= 17) {
    return DialColumnAndRow{.column = static_cast<uint8_t>(index - 14),
                            .row = 2};
  }

  if (index >= 18 && index <= 21) {
    return DialColumnAndRow{.column = static_cast<uint8_t>(index - 18),
                            .row = 3};
  }

  return std::nullopt;
}

void controllers::CmdMm1::set_layer_from_processor(const size_t layer) {
  logging::log<logging::LogLevel::Trace>("CmdMM1::set_layer_from_processor");

  if (layer >= 2) {
    logging::log<logging::LogLevel::Error>("Layer {} out of bounds", layer);

    return;
  }

  if (layer == _selected_layer_id.load()) {
    logging::log<logging::LogLevel::Debug>("Layer already same value");
    return;
  }

  _selected_layer_id.store(layer);

  logging::log<logging::LogLevel::Debug>("Set layer to {}", layer);

  add_current_state_as_feedback();
}

void controllers::CmdMm1::set_selected_channel_from_processor(
    ChannelType channel_type, size_t channel_id) {
  logging::log<logging::LogLevel::Trace>(
      "CmdMM1::set_selected_channel_from_processor");

  if (channel_type == ChannelType::CHANNEL) {
    _selected_channel_id = std::nullopt;
  } else {
    if (channel_id >= _channels.size()) {
      logging::log<logging::LogLevel::Error>(
          "Group channel id {} out of bounds", channel_id);

      return;
    }

    _selected_channel_id.store(channel_id);
  }

  add_channel_feedback();
  add_dial_mode_feedback();
  add_filter_params_feedback();
}

void controllers::CmdMm1::clear_selected_channel_from_processor() {
  logging::log<logging::LogLevel::Trace>(
      "CmdMM1::clear_selected_channel_from_processor");

  _selected_channel_id = std::nullopt;

  add_channel_feedback();
  add_dial_mode_feedback();
  add_filter_params_feedback();
}

void controllers::CmdMm1::set_selected_plugin_page_from_processor(
    size_t plugin_id, size_t page_number) {
  logging::log<logging::LogLevel::Trace>(
      "CmdMM1::set_selected_plugin_page_from_processor");
}

void controllers::CmdMm1::add_current_state_as_feedback() {
  logging::log<logging::LogLevel::Trace>(
      "CmdMm1::add_current_state_as_feedback");

  add_layer_feedback();
  add_channel_feedback();
  add_dial_mode_feedback();
  add_filter_params_feedback();
}

void controllers::CmdMm1::set_channel_node(const ChannelType channel_type,
                                           const size_t channel_id,
                                           const uint64_t object_id) {
  logging::log<logging::LogLevel::Trace>("CmdMM1::set_channel_node");

  std::lock_guard lock(_channels_mutex);
  const auto channel = channel_by_type_and_id(channel_type, channel_id);

  if (!channel) {
    return;
  }

  const auto node = _registry.get_node_by_object_id(
      object_id, ChannelType::GROUP_CHANNEL, channel_id);

  if (!node) {
    logging::log<logging::LogLevel::Error>("Couldn't find node {}", object_id);

    return;
  }

  channel->get().set_playback_node(*node);
}

void controllers::CmdMm1::set_channel_filter_node(
    const ChannelType channel_type, const size_t channel_id,
    const uint64_t object_id) {
  logging::log<::logging::LogLevel::Trace>("CmdMM1::set_channel_filter_node");

  std::lock_guard lock(_channels_mutex);
  const auto channel = channel_by_type_and_id(channel_type, channel_id);

  if (!channel) {
    return;
  }

  const auto node = _registry.get_node_by_object_id(
      object_id, ChannelType::GROUP_CHANNEL, channel_id);

  if (!node) {
    logging::log<logging::LogLevel::Error>("Couldn't find node {}", object_id);

    return;
  }

  channel->get().set_filter_node(*node);
}

void controllers::CmdMm1::set_channel_send_node(const ChannelType channel_type,
                                                const size_t channel_id,
                                                const size_t send_id,
                                                const uint64_t object_id) {
  logging::log<logging::LogLevel::Trace>("CmdMM1::set_channel_send_node");

  if (send_id >= 4) {
    logging::log<logging::LogLevel::Error>("Send {} out of bounds", send_id);

    return;
  }

  std::lock_guard lock(_channels_mutex);
  const auto channel = channel_by_type_and_id(channel_type, channel_id);

  if (!channel) {
    return;
  }

  const auto node = _registry.get_node_by_object_id(
      object_id, ChannelType::GROUP_CHANNEL, channel_id);

  if (!node) {
    logging::log<logging::LogLevel::Error>("Couldn't find node {}", object_id);

    return;
  }

  channel->get().set_send_node(send_id, *node);
}

void controllers::CmdMm1::clear_filter_parameters(
    const ChannelType channel_type, const size_t channel_id) {
  logging::log<logging::LogLevel::Trace>("CmdMM1::clear_filter_parameters");

  std::lock_guard lock(_channels_mutex);
  const auto channel = channel_by_type_and_id(channel_type, channel_id);

  if (!channel) {
    return;
  }

  channel->get().clear_parameters();
}

void controllers::CmdMm1::add_filter_parameter(const ChannelType channel_type,
                                               const size_t channel_id,
                                               const size_t plugin_id,
                                               char *name, const float min,
                                               const float max) {
  logging::log<logging::LogLevel::Trace>("CmdMM1::add_filter_parameter");

  std::lock_guard lock(_channels_mutex);
  const auto channel = channel_by_type_and_id(channel_type, channel_id);

  if (!channel) {
    return;
  }

  channel->get().add_parameter(plugin_id, name, min, max);
}

std::optional<std::reference_wrapper<controllers::Channel>>
controllers::CmdMm1::channel_by_type_and_id(const ChannelType channel_type,
                                            const size_t channel_id) {
  logging::log<logging::LogLevel::Trace>("CmdMm1::channel_by_type_and_id");

  if (channel_type == ChannelType::CHANNEL) {
    logging::log<logging::LogLevel::Trace>("Ignoring channel");

    return std::nullopt;
  }

  if (channel_id >= _number_of_channels) {
    logging::log<logging::LogLevel::Error>("Channel {} out of bounds",
                                           channel_id);

    return std::nullopt;
  }

  auto &channel = _channels[channel_id];
  return channel;
}
