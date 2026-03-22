#include "controllers/MidiMix.h"

#include "audio/pan.h"
#include "logging/log.h"
#include "pw_utils/fill_set_params_pod.h"
#include "pw_utils/fill_set_value_pod.h"
#include "pw_utils/set_pw_node_param.h"
#include "pw_utils/set_pw_node_volumes.h"

#include <spa/debug/pod.h>
#include <spa/param/props.h>
#include <spa/pod/builder.h>
#include <spa/pod/iter.h>

void controllers::MidiMix::process(const midi::Message &message) {
  logging::log<logging::LogLevel::Trace>("MidiMix::process");

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

void controllers::MidiMix::handle_note_on(const uint8_t note_number) {
  logging::log<logging::LogLevel::Trace>("MidiMix::handle_note_on");

  if (handle_channel_selection(note_number)) {
    call_channel_callback();
    add_channel_feedback();
  } else if (handle_layer_selection(note_number)) {
    call_layer_callback();
    add_layer_feedback();
    add_channel_feedback();
    add_dial_mode_feedback();
  } else if (handle_dial_mode_selection(note_number)) {
    add_dial_mode_feedback();
  } else if (handle_filter_params_increment(note_number)) {
  }
}

bool controllers::MidiMix::handle_dial_mode_selection(
    const uint8_t note_number) {
  logging::log<logging::LogLevel::Trace>("MidiMix::handle_dial_mode_selection");

  size_t channel_id{0};

  bool valid_id{false};

  if (note_number == 1) {
    channel_id = 0;
    valid_id = true;
  } else if (note_number == 4) {
    channel_id = 1;
    valid_id = true;
  } else if (note_number == 7) {
    channel_id = 2;
    valid_id = true;
  } else if (note_number == 10) {
    channel_id = 3;
    valid_id = true;
  } else if (note_number == 13) {
    channel_id = 4;
    valid_id = true;
  } else if (note_number == 16) {
    channel_id = 5;
    valid_id = true;
  } else if (note_number == 19) {
    channel_id = 6;
    valid_id = true;
  } else if (note_number == 22) {
    channel_id = 7;
    valid_id = true;
  }

  if (!valid_id) {
    logging::log<logging::LogLevel::Debug>(
        "Note number {} not used for dial mode selection", note_number);

    return false;
  }

  channel_id += layer_channel_offset();

  auto &channel = _channels[channel_id];

  channel.swap_dial_mode();

  call_dial_mode_callback(channel_id, channel);

  return true;
}

bool controllers::MidiMix::handle_filter_params_increment(
    const uint8_t note_number) {
  logging::log<logging::LogLevel::Trace>(
      "MidiMix::handle_filter_params_increment");

  size_t channel_id{0};
  bool valid_id{false};
  if (note_number == 2) {
    channel_id = 0;
    valid_id = true;
  } else if (note_number == 5) {
    channel_id = 1;
    valid_id = true;
  } else if (note_number == 8) {
    channel_id = 2;
    valid_id = true;
  } else if (note_number == 11) {
    channel_id = 3;
    valid_id = true;
  } else if (note_number == 14) {
    channel_id = 4;
    valid_id = true;
  } else if (note_number == 17) {
    channel_id = 5;
    valid_id = true;
  } else if (note_number == 20) {
    channel_id = 6;
    valid_id = true;
  } else if (note_number == 23) {
    channel_id = 7;
    valid_id = true;
  }

  if (!valid_id) {
    logging::log<logging::LogLevel::Debug>(
        "Note number {} not used for filter params target increment",
        note_number);

    return false;
  }

  channel_id += layer_channel_offset();

  auto &channel = _channels[channel_id];

  channel.increment_selected_filter_param_section();

  call_filter_params_section_select_callback(channel_id, channel);

  return true;
}

bool controllers::MidiMix::handle_layer_selection(const uint8_t note_number) {
  logging::log<logging::LogLevel::Trace>("MidiMix::handle_layer_selection");

  if (note_number == 25) {
    _selected_layer_id = 0;

    logging::log<logging::LogLevel::Debug>("Setting active layer to 0");

    return true;
  }

  if (note_number == 26) {
    _selected_layer_id = 1;

    logging::log<logging::LogLevel::Debug>("Setting active layer to 1");

    return true;
  }

  logging::log<logging::LogLevel::Debug>(
      "Note number {} not used for layer changes", note_number);

  return false;
}

bool controllers::MidiMix::handle_channel_selection(const uint8_t note_number) {
  logging::log<logging::LogLevel::Trace>("MidiMix::handle_channel_selection");

  bool handled = false;
  size_t new_channel_id{0};

  if (note_number == 3) {
    new_channel_id = 0 + layer_channel_offset();
    handled = true;
  } else if (note_number == 6) {
    new_channel_id = 1 + layer_channel_offset();
    handled = true;
  } else if (note_number == 9) {
    new_channel_id = 2 + layer_channel_offset();
    handled = true;
  } else if (note_number == 12) {
    new_channel_id = 3 + layer_channel_offset();
    handled = true;
  } else if (note_number == 15) {
    new_channel_id = 4 + layer_channel_offset();
    handled = true;
  } else if (note_number == 18) {
    new_channel_id = 5 + layer_channel_offset();
    handled = true;
  } else if (note_number == 21) {
    new_channel_id = 6 + layer_channel_offset();
    handled = true;
  } else if (note_number == 24) {
    new_channel_id = 7 + layer_channel_offset();
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

void controllers::MidiMix::add_channel_feedback() const {
  logging::log<logging::LogLevel::Trace>("MidiMix::add_channel_feedback");

  const auto channel = _selected_channel_id.load();

  if (!channel) {
    logging::log<logging::LogLevel::Debug>(
        "No active channel id, turning all rec arm lights off");

    for (int i = 0; i < 8; i++) {
      auto note_number = static_cast<uint8_t>(3 + 3 * i);
      logging::log<logging::LogLevel::Debug>(
          "Adding note on for note number {} with velocity 0", note_number);

      _feedback_channel->push(midi::NoteOnV1{
          .channel = 0, .note_number = note_number, .velocity = 0});
    }
  } else if ((_selected_layer_id == 0 && *channel < 8) ||
             (_selected_layer_id == 1 && *channel >= 8 && channel < 16)) {
    const auto highlighted_controller_channel =
        _selected_layer_id == 0 ? *channel : *channel - 8;

    logging::log<logging::LogLevel::Debug>(
        "Selected channel visible, turning on selected channel rec arm light "
        "for channel id {}",
        highlighted_controller_channel);

    for (int i = 0; i < 8; i++) {
      const auto note_number = static_cast<uint8_t>(3 + 3 * i);
      const auto velocity = static_cast<uint8_t>(
          i == static_cast<int>(highlighted_controller_channel) ? 127 : 0);

      logging::log<logging::LogLevel::Debug>(
          "Adding note on for note number {} with velocity {}", note_number,
          velocity);

      _feedback_channel->push(midi::NoteOnV1{
          .channel = 0, .note_number = note_number, .velocity = velocity});
    }
  } else {
    logging::log<logging::LogLevel::Debug>(
        "Selected channel not visible, turning all rec arm lights off");

    for (int i = 0; i < 8; i++) {
      auto note_number = static_cast<uint8_t>(3 + 3 * i);
      logging::log<logging::LogLevel::Debug>(
          "Adding note on for note number {} with velocity 0", note_number);

      _feedback_channel->push(midi::NoteOnV1{
          .channel = 0, .note_number = note_number, .velocity = 0});
    }
  }
}

void controllers::MidiMix::call_channel_callback() const {
  logging::log<logging::LogLevel::Trace>("MidiMix::call_channel_callback");

  if (const auto channel = _selected_channel_id.load())
    _channel_select_callback(*channel);
}

void controllers::MidiMix::add_layer_feedback() const {
  logging::log<logging::LogLevel::Trace>("MidiMix::add_layer_feedback");

  const auto layer = _selected_layer_id.load();

  _feedback_channel->push(midi::NoteOnV1{
      .channel = 0,
      .note_number = 25,
      .velocity = static_cast<uint8_t>(layer == 0 ? 127 : 0),
  });

  _feedback_channel->push(midi::NoteOnV1{
      .channel = 0,
      .note_number = 26,
      .velocity = static_cast<uint8_t>(layer == 1 ? 127 : 0),
  });
}

void controllers::MidiMix::call_layer_callback() const {
  logging::log<logging::LogLevel::Trace>("MidiMix::call_layer_callback");

  const auto id = _selected_layer_id.load();
  _layer_select_callback(id);
}

void controllers::MidiMix::add_dial_mode_feedback() const {
  logging::log<logging::LogLevel::Trace>("MidiMix::add_dial_mode_feedback");

  for (int i = 0; i < 8; i++) {
    const auto channel_id = i + _selected_layer_id * 8;
    auto &channel = _channels[channel_id];
    auto note_number = static_cast<uint8_t>(1 + 3 * i);
    auto velocity = channel.dial_mode() == FILTER_PARAMS ? 127 : 0;

    logging::log<logging::LogLevel::Debug>(
        "Adding note on for note number {} with velocity {}", note_number,
        velocity);

    _feedback_channel->push(midi::NoteOnV1{
        .channel = 0,
        .note_number = note_number,
        .velocity = static_cast<uint8_t>(velocity),
    });
  }
}

void controllers::MidiMix::call_dial_mode_callback(
    const size_t channel_id, const Channel &channel) const {
  logging::log<logging::LogLevel::Trace>("MidiMix::call_dial_mode_callback");

  _dial_section_mode_select_callback(channel_id, channel.dial_mode());
}

void controllers::MidiMix::call_filter_params_section_select_callback(
    size_t channel_id, const Channel &channel) const {
  logging::log<logging::LogLevel::Trace>(
      "MidiMix::call_filter_params_section_select_callback");

  _filter_params_section_select_callback(
      channel_id, channel.selected_filter_params_section());
}

void controllers::MidiMix::handle_normalized_control_change(
    const uint8_t index, const double value) const {
  logging::log<logging::LogLevel::Trace>(
      "MidiMix::handle_normalized_control_change");

  if (handle_volume_control_change(index, value))
    return;

  if (handle_send_volume_control_change(index, value))
    return;

  if (handle_filter_params_control_change(index, value))
    return;

  if (handle_master_volume_control_change(index, value))
    return;
}

bool controllers::MidiMix::handle_volume_control_change(
    const uint8_t index, const double value) const {
  logging::log<logging::LogLevel::Trace>(
      "MidiMix::handle_volume_control_change");

  bool valid_channel = false;
  size_t channel_id{0};
  if (index == 19) {
    valid_channel = true;
    channel_id = 0 + layer_channel_offset();
  } else if (index == 23) {
    valid_channel = true;
    channel_id = 1 + layer_channel_offset();
  } else if (index == 27) {
    valid_channel = true;
    channel_id = 2 + layer_channel_offset();
  } else if (index == 31) {
    valid_channel = true;
    channel_id = 3 + layer_channel_offset();
  } else if (index == 49) {
    valid_channel = true;
    channel_id = 4 + layer_channel_offset();
  } else if (index == 53) {
    valid_channel = true;
    channel_id = 5 + layer_channel_offset();
  } else if (index == 57) {
    valid_channel = true;
    channel_id = 6 + layer_channel_offset();
  } else if (index == 61) {
    valid_channel = true;
    channel_id = 7 + layer_channel_offset();
  }

  if (!valid_channel) {
    logging::log<logging::LogLevel::Debug>(
        "Control index {} not used for volume control", index);

    return false;
  }

  logging::log<logging::LogLevel::Debug>(
      "Processing volume change for channel {}", channel_id);

  auto &channel = _channels[channel_id];

  channel.set_volume(value);

  return true;
}

bool controllers::MidiMix::handle_master_volume_control_change(
    const uint8_t index, const double value) const {
  logging::log<logging::LogLevel::Trace>(
      "MidiMix::handle_master_volume_control_change");

  if (_master_channel_playback_node == nullptr) {
    logging::log<logging::LogLevel::Debug>(
        "Master channel playback node is null");

    return true;
  }

  _master_channel_playback_node->set_volume(value);

  return true;
}

bool controllers::MidiMix::handle_send_volume_control_change(
    const uint8_t index, const double value) const {
  logging::log<logging::LogLevel::Trace>(
      "MidiMix::handle_send_volume_control_change");

  const auto column_and_row = get_column_and_row_for_dial_index(index);

  if (!column_and_row) {
    logging::log<logging::LogLevel::Debug>("Control index {} is not a dial",
                                           index);

    return false;
  }

  auto &channel = _channels[column_and_row->column + layer_channel_offset()];

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

bool controllers::MidiMix::handle_filter_params_control_change(
    const uint8_t index, const double value) const {
  logging::log<logging::LogLevel::Trace>(
      "MidiMix::handle_filter_params_control_change");

  const auto column_and_row = get_column_and_row_for_dial_index(index);

  if (!column_and_row) {
    logging::log<logging::LogLevel::Debug>("Control index {} is not a dial",
                                           index);

    return false;
  }

  auto &channel = _channels[column_and_row->column + layer_channel_offset()];

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

void controllers::MidiMix::set_channel_playback_node(const size_t channel_id,
                                                     uint64_t object_id) {
  logging::log<logging::LogLevel::Trace>("MidiMix::set_playback_node");

  if (channel_id >= _channels.size()) {
    logging::log<logging::LogLevel::Error>("Channel {} out of bounds",
                                           channel_id);
    return;
  }

  auto &channel = _channels[channel_id];

  auto node = _registry.get_node_by_object_id(object_id);

  if (!node) {
    logging::log<logging::LogLevel::Error>("Couldn't get node for object id {}",
                                           object_id);
    return;
  }

  channel.set_playback_node(*node);
}

void controllers::MidiMix::set_channel_filter_node(size_t channel_id,
                                                   uint64_t object_id) {
  logging::log<logging::LogLevel::Trace>("MidiMix::set_channel_filter_node");

  if (channel_id >= _channels.size()) {
    logging::log<logging::LogLevel::Error>("Channel {} out of bounds",
                                           channel_id);
    return;
  }

  auto &channel = _channels[channel_id];

  auto node = _registry.get_node_by_object_id(object_id);

  if (!node) {
    logging::log<logging::LogLevel::Error>("Couldn't get node for object id {}",
                                           object_id);
    return;
  }

  channel.set_filter_node(*node);
}

void controllers::MidiMix::set_send_node(size_t channel_id, size_t send_id,
                                         uint64_t object_id) {
  logging::log<logging::LogLevel::Trace>("MidiMix::set_send_node");

  if (channel_id >= _channels.size()) {
    logging::log<logging::LogLevel::Error>("Channel {} out of bounds",
                                           channel_id);
    return;
  }

  auto &channel = _channels[channel_id];

  auto node = _registry.get_node_by_object_id(object_id);

  if (!node) {
    logging::log<logging::LogLevel::Error>("Couldn't get node for object id {}",
                                           object_id);
    return;
  }

  channel.set_send_node(send_id, *node);
}

std::optional<controllers::DialColumnAndRow>
controllers::MidiMix::get_column_and_row_for_dial_index(const uint8_t index) {
  if (index >= 16 && index <= 18) {
    return DialColumnAndRow{.column = 0,
                            .row = static_cast<uint8_t>(index - 16)};
  } else if (index >= 20 && index <= 22) {
    return DialColumnAndRow{.column = 1,
                            .row = static_cast<uint8_t>(index - 20)};
  } else if (index >= 24 && index <= 26) {
    return DialColumnAndRow{.column = 2,
                            .row = static_cast<uint8_t>(index - 24)};
  } else if (index >= 28 && index <= 30) {
    return DialColumnAndRow{.column = 3,
                            .row = static_cast<uint8_t>(index - 28)};
  } else if (index >= 46 && index <= 48) {
    return DialColumnAndRow{.column = 4,
                            .row = static_cast<uint8_t>(index - 46)};
  } else if (index >= 50 && index <= 52) {
    return DialColumnAndRow{.column = 5,
                            .row = static_cast<uint8_t>(index - 50)};
  } else if (index >= 54 && index <= 56) {
    return DialColumnAndRow{.column = 6,
                            .row = static_cast<uint8_t>(index - 54)};
  } else if (index >= 58 && index <= 60) {
    return DialColumnAndRow{.column = 7,
                            .row = static_cast<uint8_t>(index - 58)};
  }

  return std::nullopt;
}
