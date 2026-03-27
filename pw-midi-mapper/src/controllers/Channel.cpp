#include "controllers/Channel.h"

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

void controllers::Channel::swap_dial_mode() {
  logging::log<logging::LogLevel::Trace>("Channel::swap_dial_mode");

  _dial_mode = _dial_mode == SENDS ? FILTER_PARAMS : SENDS;

  logging::log<logging::LogLevel::Debug>("Set dial mode {} for channel id {}",
                                         to_string(_dial_mode), _channel_id);
}

controllers::DialMode controllers::Channel::dial_mode() const {
  return _dial_mode;
}

void controllers::Channel::increment_selected_filter_param_section() {
  logging::log<logging::LogLevel::Trace>(
      "Channel::increment_selected_filter_param_section");

  _selected_filter_params_section = (_selected_filter_params_section + 1) % 3;

  logging::log<logging::LogLevel::Debug>(
      "Set selected filter param section {} for channel id {}",
      _selected_filter_params_section.load(), _channel_id);
}

uint8_t controllers::Channel::selected_filter_params_section() const {
  return _selected_filter_params_section;
}

void controllers::Channel::set_volume(const double value) const {
  logging::log<logging::LogLevel::Trace>("Channel::set_volume");

  if (_playback_node == nullptr) {
    logging::log<logging::LogLevel::Debug>("Channel playback node is null");
    return;
  }

  _playback_node->set_volume(value);
}

void controllers::Channel::set_send_trim(size_t send_id, double value) const {
  logging::log<logging::LogLevel::Trace>("Channel::set_send_trim");

  if (send_id >= 4) {
    logging::log<logging::LogLevel::Error>("Channel {} send {} out of bounds",
                                           _channel_id, send_id);
    return;
  }

  const auto send = _sends[send_id];
  if (send == nullptr) {
    logging::log<logging::LogLevel::Debug>("Channel {} send {} node is null",
                                           _channel_id, send_id);
    return;
  }

  send->set_channel_volumes(
      {static_cast<float>(value), static_cast<float>(value)});
}

void controllers::Channel::set_parameter_for_selected_section(
    size_t parameter_id, const double value) const {
  logging::log<logging::LogLevel::Trace>(
      "Channel::set_parameter_for_selected_section");

  if (_filter_node == nullptr) {
    logging::log<logging::LogLevel::Debug>("Filter capture node is null");
    return;
  }

  if (parameter_id >= 4) {
    logging::log<logging::LogLevel::Debug>("Parameter {} out of bounds",
                                           parameter_id);
    return;
  }

  auto parameter = _plugins[_selected_filter_params_section][parameter_id];

  if (!parameter) {
    logging::log<logging::LogLevel::Debug>(
        "Section {}, parameter {} not set",
        _selected_filter_params_section.load(), parameter_id);
  }

  logging::log<logging::LogLevel::Debug>("Processing parameter {}",
                                         parameter->name);

  const auto new_value =
      parameter->max + value * (parameter->max - parameter->min);

  logging::log<logging::LogLevel::Debug>(
      "Setting section {}, parameter {} to value {}",
      _selected_filter_params_section.load(), parameter_id, new_value);

  _filter_node->set_param(parameter->name, new_value);
}
void controllers::Channel::set_send_node(size_t send_id, registry::Node *node) {
  logging::log<logging::LogLevel::Debug>("Channel::set_send_node");

  if (send_id >= _sends.size()) {
    logging::log<logging::LogLevel::Error>("Channel {} send {} out of bounds",
                                           _channel_id, send_id);

    return;
  }

  _sends[send_id] = node;
}

void controllers::Channel::clear_parameters() {
  logging::log<logging::LogLevel::Debug>("Channel::clear_parameters");

  for (auto &plugin : _plugins) {
    for (size_t parameter_id = 0; parameter_id < _plugins.size();
         parameter_id++) {
      plugin[parameter_id] = std::nullopt;
    }
  }
}

void controllers::Channel::add_parameter(const size_t plugin_id, char *name,
                                         const float min, const float max) {
  logging::log<logging::LogLevel::Debug>("Channel::add_parameter");

  if (plugin_id >= _plugins.size()) {
    logging::log<logging::LogLevel::Error>("Plugin {} out of bounds",
                                           plugin_id);
    return;
  }

  auto &plugin = _plugins[plugin_id];
  for (size_t parameter_id = 0; parameter_id < _plugins.size();
       parameter_id++) {
    if (!plugin[parameter_id]) {
      logging::log<logging::LogLevel::Debug>(
          "Setting parameter {} for plugin {} to {}", parameter_id, plugin_id,
          name);

      plugin[parameter_id] = Parameter{
          .name = name,
          .value = 0.0f,
          .max = max,
          .min = min,
      };
      return;
    }
  }

  logging::log<logging::LogLevel::Debug>("Couldn't find parameter space for {}",
                                         name);
}
