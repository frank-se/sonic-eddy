#include "registry/Node.h"

#include "pw_utils/set_pw_node_volumes.h"

#include <ranges>

void registry::Node::set_channel_volumes(const std::array<float, 2> volumes) {
  logging::log<logging::LogLevel::Trace>("Node::set_channel_volumes");
  if (!_volume_catch_up_handler.should_update(volumes[0], _left)) {

    logging::log<logging::LogLevel::Debug>(
        "Node {} needs to catch up from {} to {}", _object_id, volumes[0],
        _left.load());

    _controller_update_callback(_channel_type, _channel_id, _object_id,
                                "send_channel_volumes", volumes[0],
                                _left.load(), true);

    return;
  }

  pw_utils::set_pw_node_volume(_loop, _node, volumes);

  _controller_update_callback(_channel_type, _channel_id, _object_id,
                              "send_channel_volumes", volumes[0], _left.load(),
                              false);
}

void registry::Node::set_param(const std::string &name,
                               const float value) const {
  logging::log<logging::LogLevel::Trace>("Node::set_param");

  pw_utils::set_pw_node_param(_loop, _node, name, value);
}

void registry::Node::set_param(const controllers::Parameter &parameter,
                               const float normalized_controller_value) {
  logging::log<logging::LogLevel::Trace>("Node::set_param");

  const auto parameter_value = this->parameter_value(parameter.name);

  if (!parameter_value) {
    logging::log<logging::LogLevel::Error>("Parameter {} set but value unknown",
                                           parameter.name);

    return;
  }

  const auto normalized_current_parameter_value =
      (*parameter_value - parameter.min) / (parameter.max - parameter.min);

  const auto new_value = parameter.min + normalized_controller_value *
                                             (parameter.max - parameter.min);

  if (!parameter.catch_up_handler->should_update(
          normalized_controller_value, normalized_current_parameter_value)) {
    logging::log<logging::LogLevel::Debug>(
        "Parameter {} for channel {} needs to catch up from {} to {}",
        parameter.name, _channel_id, new_value, *parameter_value);

    _controller_update_callback(
        _channel_type, _channel_id, object_id(), parameter.name.c_str(),
        normalized_controller_value, normalized_current_parameter_value, true);

    return;
  }

  logging::log<logging::LogLevel::Debug>("Setting parameter {} to value {}",
                                         parameter.name, new_value);

  set_param(parameter.name, new_value);

  _controller_update_callback(
      _channel_type, _channel_id, object_id(), parameter.name.c_str(),
      normalized_controller_value, normalized_current_parameter_value, false);
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

void registry::Node::set_volume(const double value) {
  logging::log<logging::LogLevel::Trace>("Node::set_volume");

  const auto current_pan_and_volume = pan_and_volume();

  if (!_volume_catch_up_handler.should_update(static_cast<float>(value),
                                              current_pan_and_volume->volume)) {

    logging::log<logging::LogLevel::Debug>(
        "Node {} needs to catch up from {} to {}", _object_id, value,
        current_pan_and_volume->volume);

    logging::log<logging::LogLevel::Debug>(
        "Calling controller update callback");

    _controller_update_callback(_channel_type, _channel_id, _object_id,
                                "channel_volumes", static_cast<float>(value),
                                current_pan_and_volume->volume, true);

    return;
  }

  if (value == 0) {
    pw_utils::set_pw_node_volume(_loop, _node, {0.0f, 0.0f});
    return;
  }

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

  pw_utils::set_pw_node_volume(_loop, _node, gains);

  logging::log<logging::LogLevel::Debug>("Calling controller update callback");

  _controller_update_callback(_channel_type, _channel_id, _object_id,
                              "channel_volumes", static_cast<float>(value),
                              current_pan_and_volume->volume, false);
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

spa_pod *get_pod_body(const spa_pod *source_pod) {
  return reinterpret_cast<spa_pod *>(reinterpret_cast<uintptr_t>(source_pod) +
                                     static_cast<ptrdiff_t>(sizeof(spa_pod)));
}

void registry::Node::on_node_params_changed(void *user_data,
                                            int sequence_number, uint32_t id,
                                            uint32_t index, uint32_t next,
                                            const spa_pod *pod) {
  logging::log<logging::LogLevel::Trace>("Node::on_node_params_changed");

  auto *node = static_cast<Node *>(user_data);

  if (SPA_POD_TYPE(pod) == SPA_TYPE_Object) {
    logging::log<logging::LogLevel::Trace>("Processing SPA_TYPE_Object");

    const auto channel_volumes_property =
        spa_pod_find_prop(pod, nullptr, SPA_PROP_channelVolumes);

    if (channel_volumes_property == nullptr) {
      logging::log<logging::LogLevel::Trace>(
          "No channel volume message in updates, skipping to parameters");
    } else {
      const auto channel_volumes_array = &channel_volumes_property->value;
      if (!spa_pod_is_array(channel_volumes_array))
        return;

      const auto number_of_channels =
          SPA_POD_ARRAY_N_VALUES(channel_volumes_array);

      if (number_of_channels != 2) {
        logging::log<logging::LogLevel::Debug>(
            "Unexpected number of channels {}", number_of_channels);

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

    const auto params_pod = spa_pod_find_prop(pod, nullptr, SPA_PROP_params);

    auto pod_body_pointer = get_pod_body(&params_pod->value);

    spa_pod *child = nullptr;
    size_t child_index = 0;

    std::lock_guard params_lock{node->_param_values_mutex};

    std::optional<std::string> parameter_name = std::nullopt;
    SPA_POD_FOREACH(pod_body_pointer, SPA_POD_BODY_SIZE(&params_pod->value),
                    child) {
      if (child_index % 2 == 0) {
        const char *name_c = nullptr;
        spa_pod_get_string(child, &name_c);

        if (name_c == nullptr) {
          logging::log<logging::LogLevel::Warning>(
              "Spa pod struct entry without name");
          child_index++;
          continue;
        }

        std::string_view name(name_c);

        logging::log<logging::LogLevel::Trace>("Processing parameter {}", name);

        if (name.find_first_of(':') == std::string::npos) {
          logging::log<logging::LogLevel::Debug>(
              "Parameter {} is not filter chain param, ignoring", name);

          child_index++;
          continue;
        }

        parameter_name = name;
      } else if (parameter_name) {
        float value{0.0f};
        spa_pod_get_float(child, &value);
        node->_param_values[parameter_name.value()] = value;

        logging::log<logging::LogLevel::Debug>("Updated parameter {} to {}",
                                               parameter_name.value(), value);

        parameter_name = std::nullopt;
      }

      child_index++;
    }
  }
}

std::optional<float> registry::Node::parameter_value(const std::string &name) {
  std::lock_guard params_lock{_param_values_mutex};
  if (const auto it = _param_values.find(name); it != _param_values.end())
    return it->second;

  return std::nullopt;
}
