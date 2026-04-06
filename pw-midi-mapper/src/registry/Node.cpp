#include "registry/Node.h"

#include "pw_utils/set_pw_node_volumes.h"

void registry::Node::set_channel_volumes(
    const std::array<float, 2> volumes) const {
  logging::log<logging::LogLevel::Trace>("Node::set_channel_volumes");

  pw_utils::set_pw_node_volume(_loop, _node, volumes);
}

void registry::Node::set_param(const std::string &name,
                               const float value) const {
  logging::log<logging::LogLevel::Trace>("Node::set_param");

  pw_utils::set_pw_node_param(_loop, _node, name, value);
}

std::optional<std::array<float, 2>> registry::Node::channel_volumes() const {
  if (_subscribed_to_param_updates == false)
    return std::nullopt;

  return std::array<float, 2>{_left, _right};
}

std::optional<audio::pan::PanAndVolume> registry::Node::pan_and_volume() const {
  if (_subscribed_to_param_updates == false)
    return std::nullopt;

  return audio::pan::get_pan_and_volume_from_gains(*channel_volumes());
}

void registry::Node::set_volume(const double value) const {
  logging::log<logging::LogLevel::Trace>("Node::set_volume");

  if (value == 0) {
    set_channel_volumes({0.0f, 0.0f});
    return;
  }

  const auto current_pan_and_volume = pan_and_volume();

  if (!current_pan_and_volume) {
    logging::log<logging::LogLevel::Error>("Couldn't calculate pan and volume");
    return;
  }

  const auto gains = audio::pan::get_gains_from_pan_and_volume(
      audio::pan::PanAndVolume{.pan = current_pan_and_volume->pan,
                               .volume = static_cast<float>(value)});

  logging::log<logging::LogLevel::Debug>(
      "Calculated left {} and right {} gain for node {}", gains[0], gains[1],
      _object_id);

  set_channel_volumes(gains);
}

void registry::Node::subscribe_to_param_updates() {
  logging::log<logging::LogLevel::Trace>("Node::subscribe_to_param_updates");

  if (_subscribed_to_param_updates || _node == nullptr)
    return;

  pw_node_add_listener(_node, &_node_listener, &_node_events, this);

  std::array<uint32_t, 1> parameter_ids = {SPA_PARAM_Props};
  pw_node_subscribe_params(_node, parameter_ids.data(), parameter_ids.size());

  pw_node_enum_params(_node, 0, PW_ID_ANY, 0, 0, nullptr);

  _subscribed_to_param_updates = true;
}

void registry::Node::on_channel_playback_node_params_changed(
    void *user_data, int sequence_number, uint32_t id, uint32_t index,
    uint32_t next, const spa_pod *pod) {
  logging::log<logging::LogLevel::Trace>(
      "Node::on_channel_playback_node_params_changed");

  auto *node = static_cast<Node *>(user_data);

  if (SPA_POD_TYPE(pod) == SPA_TYPE_Object) {
    logging::log<logging::LogLevel::Trace>("Processing SPA_TYPE_Object");

    const auto channel_volumes_property =
        spa_pod_find_prop(pod, nullptr, SPA_PROP_channelVolumes);

    if (channel_volumes_property == nullptr) {
      logging::log<logging::LogLevel::Trace>(
          "No channel volume message in updates");

      return;
    }

    const auto channel_volumes_array = &channel_volumes_property->value;
    if (!spa_pod_is_array(channel_volumes_array))
      return;

    const auto number_of_channels =
        SPA_POD_ARRAY_N_VALUES(channel_volumes_array);

    if (number_of_channels != 2) {
      logging::log<logging::LogLevel::Debug>("Unexpected number of channels {}",
                                             number_of_channels);

      return;
    }

    if (SPA_POD_ARRAY_VALUE_TYPE(channel_volumes_array) != SPA_TYPE_Float)
      return;

    const auto channel_volumes =
        static_cast<float *>(SPA_POD_ARRAY_VALUES(channel_volumes_array));

    logging::log<logging::LogLevel::Debug>(
        "Channel volumes: left {}, right {} for node {}", channel_volumes[0],
        channel_volumes[1], node->object_id());

    node->_left = channel_volumes[0];
    node->_right = channel_volumes[1];
  }
}
