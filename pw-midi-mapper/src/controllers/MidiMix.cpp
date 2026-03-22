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

constexpr std::string to_string(const controllers::DialMode dial_mode) {
  switch (dial_mode) {
  case controllers::FILTER_PARAMS:
    return "Filter Params";
  case controllers::SENDS:
    return "Sends";
  default:
    return "Unknown";
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

  channel_id += _selected_layer_id * 8;

  auto &channel = _channels[channel_id];
  const auto new_dial_mode =
      channel.dials_mode == SENDS ? FILTER_PARAMS : SENDS;

  logging::log<logging::LogLevel::Debug>(
      "Setting dial mode {} for channel id {}", to_string(new_dial_mode),
      channel_id);

  channel.dials_mode = new_dial_mode;

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

  channel_id += _selected_layer_id * 8;

  auto &channel = _channels[channel_id];

  auto new_selected_filter_param_section =
      (channel.selected_filter_params_section + 1) % 3;

  logging::log<logging::LogLevel::Debug>(
      "Setting selected filter param section {} for channel id {}",
      new_selected_filter_param_section, channel_id);

  channel.selected_filter_params_section = new_selected_filter_param_section;

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
    new_channel_id = 0 + _selected_layer_id * 8;
    handled = true;
  } else if (note_number == 6) {
    new_channel_id = 1 + _selected_layer_id * 8;
    handled = true;
  } else if (note_number == 9) {
    new_channel_id = 2 + _selected_layer_id * 8;
    handled = true;
  } else if (note_number == 12) {
    new_channel_id = 3 + _selected_layer_id * 8;
    handled = true;
  } else if (note_number == 15) {
    new_channel_id = 4 + _selected_layer_id * 8;
    handled = true;
  } else if (note_number == 18) {
    new_channel_id = 5 + _selected_layer_id * 8;
    handled = true;
  } else if (note_number == 21) {
    new_channel_id = 6 + _selected_layer_id * 8;
    handled = true;
  } else if (note_number == 24) {
    new_channel_id = 7 + _selected_layer_id * 8;
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
  _feedback_channel->push(
      midi::NoteOnV1{.channel = 0,
                     .note_number = 25,
                     .velocity = static_cast<uint8_t>(layer == 0 ? 127 : 0)});
  _feedback_channel->push(
      midi::NoteOnV1{.channel = 0,
                     .note_number = 26,
                     .velocity = static_cast<uint8_t>(layer == 1 ? 127 : 0)});
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
    auto velocity = channel.dials_mode == FILTER_PARAMS ? 127 : 0;

    logging::log<logging::LogLevel::Debug>(
        "Adding note on for note number {} with velocity {}", note_number,
        velocity);

    _feedback_channel->push(
        midi::NoteOnV1{.channel = 0,
                       .note_number = note_number,
                       .velocity = static_cast<uint8_t>(velocity)});
  }
}

void controllers::MidiMix::call_dial_mode_callback(
    const size_t channel_id, const Channel &channel) const {
  logging::log<logging::LogLevel::Trace>("MidiMix::call_dial_mode_callback");

  _dial_section_mode_select_callback(channel_id, channel.dials_mode);
}

void controllers::MidiMix::call_filter_params_section_select_callback(
    size_t channel_id, const Channel &channel) const {
  logging::log<logging::LogLevel::Debug>(
      "MidiMix::call_filter_params_section_select_callback");

  _filter_params_section_select_callback(
      channel_id, channel.selected_filter_params_section);
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
    channel_id = 0 + _selected_layer_id * 8;
  } else if (index == 23) {
    valid_channel = true;
    channel_id = 1 + _selected_layer_id * 8;
  } else if (index == 27) {
    valid_channel = true;
    channel_id = 2 + _selected_layer_id * 8;
  } else if (index == 31) {
    valid_channel = true;
    channel_id = 3 + _selected_layer_id * 8;
  } else if (index == 49) {
    valid_channel = true;
    channel_id = 4 + _selected_layer_id * 8;
  } else if (index == 53) {
    valid_channel = true;
    channel_id = 5 + _selected_layer_id * 8;
  } else if (index == 57) {
    valid_channel = true;
    channel_id = 6 + _selected_layer_id * 8;
  } else if (index == 61) {
    valid_channel = true;
    channel_id = 7 + _selected_layer_id * 8;
  }

  if (!valid_channel) {
    logging::log<logging::LogLevel::Debug>(
        "Control index {} not used for volume control", index);

    return false;
  }

  logging::log<logging::LogLevel::Debug>(
      "Processing volume change for channel {}", channel_id);

  const auto &channel = _channels[channel_id];

  const auto gains =
      audio::pan::get_gains_from_pan_and_volume(audio::pan::PanAndVolume{
          .pan = channel.pan, .volume = static_cast<float>(value)});

  logging::log<logging::LogLevel::Debug>("Calculated left {} and right {} gain",
                                         gains[0], gains[1]);

  if (channel.channel_playback_node.node == nullptr) {
    logging::log<logging::LogLevel::Error>("Channel playback node is null");

    return true;
  }

  pw_utils::set_pw_node_volume(_loop, channel.channel_playback_node.node,
                               gains);

  return true;
}

bool controllers::MidiMix::handle_master_volume_control_change(
    const uint8_t index, const double value) const {
  logging::log<logging::LogLevel::Trace>(
      "MidiMix::handle_master_volume_control_change");

  const auto gains =
      audio::pan::get_gains_from_pan_and_volume(audio::pan::PanAndVolume{
          .pan = master_pan, .volume = static_cast<float>(value)});

  logging::log<logging::LogLevel::Debug>("Calculated left {} and right {} gain",
                                         gains[0], gains[1]);

  if (master_channel_playback_node.node == nullptr) {
    logging::log<logging::LogLevel::Error>(
        "Master channel playback node is null");

    return true;
  }

  pw_utils::set_pw_node_volume(_loop, master_channel_playback_node.node, gains);

  return true;
}

bool controllers::MidiMix::handle_send_volume_control_change(
    const uint8_t index, const double value) const {
  logging::log<logging::LogLevel::Trace>("MidiMix::handle_send_control_change");

  const auto channel_id = get_channel_id_for_dial_index(index);

  if (!channel_id) {
    logging::log<logging::LogLevel::Debug>("Control index {} is not a dial",
                                           index);

    return false;
  }

  auto &channel = _channels[channel_id->controller_channel_id];

  if (channel.dials_mode == FILTER_PARAMS) {
    logging::log<logging::LogLevel::Debug>(
        "Dials mode for channel {} is set to FILTER_PARAMS, ignoring",
        channel_id->controller_channel_id);

    return false;
  }

  auto &send_channel = channel.send_channels[channel_id->dial_row];

  logging::log<logging::LogLevel::Debug>("Setting send volume {}", value);

  auto *node = send_channel.node;

  if (node == nullptr) {
    logging::log<logging::LogLevel::Error>(
        "Channel id {}, send channel {} node is null",
        channel_id->controller_channel_id, channel_id->dial_row);

    return true;
  }

  pw_utils::set_pw_node_volume(
      _loop, node, {static_cast<float>(value), static_cast<float>(value)});

  return true;
}

bool controllers::MidiMix::handle_filter_params_control_change(
    const uint8_t index, const double value) const {
  logging::log<logging::LogLevel::Trace>(
      "MidiMix::handle_filter_params_control_change");

  const auto channel_id = get_channel_id_for_dial_index(index);

  if (!channel_id) {
    logging::log<logging::LogLevel::Debug>("Control index {} is not a dial",
                                           index);

    return false;
  }

  auto &channel = _channels[channel_id->controller_channel_id];

  if (channel.dials_mode == SENDS) {
    logging::log<logging::LogLevel::Debug>(
        "Dials mode for channel {} is set to SENDS, ignoring",
        channel_id->controller_channel_id);

    return false;
  }

  auto parameter_group_id = channel.selected_filter_params_section.load();

  if (parameter_group_id >= channel.parameters.size()) {
    logging::log<logging::LogLevel::Error>(
        "Selected filter params section greater than number of parameter "
        "groups");

    return true;
  }

  logging::log<logging::LogLevel::Debug>("Using parameter group id {}",
                                         parameter_group_id);

  const auto parameter_group = channel.parameters[parameter_group_id];
  const auto row = channel_id->dial_row;

  if (row >= parameter_group.size()) {
    logging::log<logging::LogLevel::Error>(
        "Row id {} higher than number of parameters", row);

    return true;
  }

  const auto &parameter = parameter_group[row];

  if (!parameter) {
    logging::log<logging::LogLevel::Warning>(
        "Parameter not set - channel {}, row {}",
        channel_id->controller_channel_id, channel_id->dial_row);

    return true;
  }

  logging::log<logging::LogLevel::Debug>("Processing parameter {}",
                                         parameter->name);

  const auto new_value =
      parameter->max + value * (parameter->max - parameter->min);

  logging::log<logging::LogLevel::Debug>("Calculated new value {}", new_value);

  auto node = channel.channel_filter_node.node;
  if (node == nullptr) {
    logging::log<logging::LogLevel::Warning>(
        "Filter node is null for channel {}",
        channel_id->controller_channel_id);

    return true;
  }

  pw_utils::set_pw_node_param(_loop, node, parameter->name,
                              static_cast<float>(new_value));

  return true;
}

void controllers::MidiMix::set_channel_playback_node(const size_t channel_id,
                                                     uint64_t object_id) {
  logging::log<logging::LogLevel::Trace>("MidiMix::set_channel_playback_node");

  if (channel_id >= _channels.size())
    return;

  auto &channel = _channels[channel_id];

  channel.channel_playback_node.object_id = object_id;

  pw_loop_invoke(
      pw_main_loop_get_loop(_loop),
      [](spa_loop *loop, bool async, std::uint32_t seq, const void *data,
         size_t size, void *user_data) {
        const auto channel_id_local = *static_cast<const size_t *>(data);
        const auto controller = static_cast<MidiMix *>(user_data);
        controller->bind_channel_playback_node(channel_id_local);
        return 0;
      },
      0, &channel_id, sizeof(channel_id), true, this);
}

static constexpr pw_node_events node_events = {
    .version = PW_VERSION_NODE_EVENTS,
    .param = controllers::MidiMix::on_channel_playback_node_params_changed};

void controllers::MidiMix::bind_channel_playback_node(const size_t channel_id) {
  logging::log<logging::LogLevel::Trace>("MidiMix::bind_channel_playback_node");

  if (channel_id >= _channels.size())
    return;

  auto &channel = _channels[channel_id];

  if (!channel.channel_playback_node.object_id)
    return;

  channel.channel_playback_node.node = static_cast<pw_node *>(
      pw_registry_bind(_registry, *channel.channel_playback_node.object_id,
                       PW_TYPE_INTERFACE_Node, PW_VERSION_NODE, 0));

  pw_node_add_listener(channel.channel_playback_node.node,
                       &channel.channel_playback_node.node_listener,
                       &node_events, &channel);

  std::array<uint32_t, 1> parameter_ids = {SPA_PARAM_Props};
  pw_node_subscribe_params(channel.channel_playback_node.node,
                           parameter_ids.data(), parameter_ids.size());

  pw_node_enum_params(channel.channel_playback_node.node, 0, PW_ID_ANY, 0, 0,
                      nullptr);
}

void controllers::MidiMix::on_channel_playback_node_params_changed(
    void *user_data, int sequence_number, uint32_t id, uint32_t index,
    uint32_t next, const spa_pod *pod) {
  logging::log<logging::LogLevel::Trace>(
      "MidiMix::on_channel_playback_node_params_changed");

  auto *channel = static_cast<Channel *>(user_data);

  if (SPA_POD_TYPE(pod) == SPA_TYPE_Object) {
    const auto channel_volumes_property =
        spa_pod_find_prop(pod, nullptr, SPA_PROP_channelVolumes);

    if (channel_volumes_property == nullptr)
      return;

    const auto channel_volumes_array = &channel_volumes_property->value;
    if (!spa_pod_is_array(channel_volumes_array))
      return;

    const auto number_of_channels =
        SPA_POD_ARRAY_N_VALUES(channel_volumes_array);

    if (number_of_channels != 2)
      return;

    if (SPA_POD_ARRAY_VALUE_TYPE(channel_volumes_array) != SPA_TYPE_Float)
      return;

    const auto channel_volumes =
        static_cast<float *>(SPA_POD_ARRAY_VALUES(channel_volumes_array));

    const auto left = channel_volumes[0];
    const auto right = channel_volumes[1];

    if (left < 0.0002 && right < 0.0002) {
      channel->volume = 0.0f;
      return;
    }

    auto [pan, volume] =
        audio::pan::get_pan_and_volume_from_gains({left, right});

    channel->pan = pan;
    channel->volume = volume;
  }
}

void controllers::MidiMix::set_channel_filter_playback_node(
    size_t channel_id, uint64_t object_id) {
  logging::log<logging::LogLevel::Trace>(
      "MidiMix::set_channel_filter_playback_node");
}

std::optional<controllers::MidiMix::DialChannelAndRow>
controllers::MidiMix::get_channel_id_for_dial_index(const uint8_t index) {
  if (index >= 16 && index <= 18) {
    return DialChannelAndRow{.controller_channel_id = 0,
                             .dial_row = static_cast<uint8_t>(index - 16)};
  } else if (index >= 20 && index <= 22) {
    return DialChannelAndRow{.controller_channel_id = 1,
                             .dial_row = static_cast<uint8_t>(index - 20)};
  } else if (index >= 24 && index <= 26) {
    return DialChannelAndRow{.controller_channel_id = 2,
                             .dial_row = static_cast<uint8_t>(index - 24)};
  } else if (index >= 28 && index <= 30) {
    return DialChannelAndRow{.controller_channel_id = 3,
                             .dial_row = static_cast<uint8_t>(index - 28)};
  } else if (index >= 46 && index <= 48) {
    return DialChannelAndRow{.controller_channel_id = 4,
                             .dial_row = static_cast<uint8_t>(index - 46)};
  } else if (index >= 50 && index <= 52) {
    return DialChannelAndRow{.controller_channel_id = 5,
                             .dial_row = static_cast<uint8_t>(index - 50)};
  } else if (index >= 54 && index <= 56) {
    return DialChannelAndRow{.controller_channel_id = 6,
                             .dial_row = static_cast<uint8_t>(index - 54)};
  } else if (index >= 58 && index <= 60) {
    return DialChannelAndRow{.controller_channel_id = 7,
                             .dial_row = static_cast<uint8_t>(index - 58)};
  }

  return std::nullopt;
}
