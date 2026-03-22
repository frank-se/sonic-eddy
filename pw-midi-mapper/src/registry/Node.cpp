#include "registry/Node.h"

std::optional<std::array<float, 2>> registry::Node::channel_volumes() const {
  if (_subscribed_to_param_updates == false)
    return std::nullopt;

  return std::array<float, 2>{_left, _right};
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

    node->_left = channel_volumes[0];
    node->_right = channel_volumes[1];
  }
}
