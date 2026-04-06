#include "controllers/FaderfoxPc4.h"

void controllers::FaderfoxPc4::process(const midi::Message &message) {
  logging::log<logging::LogLevel::Trace>("FaderfoxPc4::process");

  std::visit(
      [this](const auto &arg) {
        using T = std::decay_t<decltype(arg)>;
        if constexpr (std::is_same_v<T, midi::ControlChangeV1>) {
          handle_normalized_control_change(arg.index,
                                           midi::normalized_cc_value(arg));
        } else if constexpr (std::is_same_v<T, midi::ControlChangeV2>) {
          handle_normalized_control_change(arg.index,
                                           midi::normalized_cc_value(arg));
        } else {
          logging::log<logging::LogLevel::Trace>("Ignoring non control change");
        }
      },
      message);
}

void controllers::FaderfoxPc4::handle_normalized_control_change(size_t index,
                                                                double value) {
  logging::log<logging::LogLevel::Trace>(
      "FaderfoxPc4::handle_normalized_control_change");

  if (index < 1 || index > 24) {
    logging::log<logging::LogLevel::Trace>("CC value not handled by fader fox");

    return;
  }
  const size_t parameter_id = index - 1;
  const size_t row = parameter_id / 4;
  const size_t column = parameter_id - (row * 4);

  auto &[filter_node, plugins] = _channels[_selected_channel_id];

  if (filter_node == nullptr) {
    logging::log<logging::LogLevel::Warning>("Channel {} filter node is null",
                                             _selected_channel_id.load());

    return;
  }

  auto &parameter =
      plugins[_selected_plugin_id][_selected_parameter_page][row][column];

  if (!parameter) {
    logging::log<logging::LogLevel::Debug>(
        "Channel {}, plugin {}, parameter page {}, index {}, row {}, "
        "column {} not set",
        _selected_channel_id.load(), _selected_plugin_id.load(),
        _selected_parameter_page.load(), index, row, column);

    return;
  }

  auto param_value = parameter->min + (parameter->max - parameter->min) * value;

  logging::log<logging::LogLevel::Debug>(
      "Setting channel {}, plugin {}, parameter page {}, index {}, row {}, "
      "column {} to {}, normalized: {}",
      _selected_channel_id.load(), _selected_plugin_id.load(),
      _selected_parameter_page.load(), index, row, column, param_value, value);

  filter_node->set_param(parameter->name, static_cast<float>(param_value));
}

void controllers::FaderfoxPc4::set_layer_from_processor(
    size_t layer) { /* NOOP */ }

void controllers::FaderfoxPc4::set_parameter(size_t channel_id,
                                             size_t plugin_id,
                                             size_t parameter_id,
                                             std::string &name, const float max,
                                             const float min) {
  logging::log<logging::LogLevel::Trace>("FaderfoxPc4::set_parameter");

  if (channel_id > _channels.size() - 1) {
    logging::log<logging::LogLevel::Error>("Channel id {} out of bounds",
                                           channel_id);

    return;
  }

  if (plugin_id > number_of_plugins - 1) {
    logging::log<logging::LogLevel::Error>("Plugin id {} out of bounds",
                                           plugin_id);

    return;
  }

  if (parameter_id > parameters_per_plugin - 1) {
    logging::log<logging::LogLevel::Error>("Parameter id {} out of bounds",
                                           parameter_id);

    return;
  }

  std::lock_guard lock(_channels_mutex);
  auto &[filter_node, plugins] = _channels[channel_id];
  auto &plugin = plugins[plugin_id];
  const size_t parameter_page_id = parameter_id / 24;
  auto &parameter_page = plugin[parameter_page_id];
  const size_t local_parameter_id = parameter_id - (parameter_page_id * 24);
  const size_t row = local_parameter_id / 4;
  const size_t column = local_parameter_id - (row * 4);
  auto &parameter = parameter_page[row][column];

  logging::log<logging::LogLevel::Debug>(
      "Setting channel {}, plugin {}, parameter {}, row {}, "
      "column {} to {}",
      channel_id, plugin_id, parameter_id, row, column, name);

  if (!parameter) {
    parameter = Parameter{
        .name = name,
        .value = 0.0f,
        .max = max,
        .min = min,
    };

    return;
  }

  parameter->name = name;
  parameter->max = max;
  parameter->min = min;
  parameter->value = 0;
}

void controllers::FaderfoxPc4::set_filter_node(size_t channel_id,
                                               registry::Node *node) {
  logging::log<logging::LogLevel::Trace>("FaderfoxPc4::set_filter_node");

  if (channel_id > _channels.size() - 1) {
    logging::log<logging::LogLevel::Error>("Channel id {} out of bounds",
                                           channel_id);

    return;
  }

  std::lock_guard lock(_channels_mutex);

  _channels[channel_id].filter_node = node;
}

void controllers::FaderfoxPc4::set_selected_parameter_page(
    const size_t page_number) {
  logging::log<logging::LogLevel::Trace>(
      "FaderfoxPc4::set_selected_parameter_page");

  if (page_number > number_of_parameter_pages - 1) {
    logging::log<logging::LogLevel::Error>("Parameter page {} out of bounds",
                                           page_number);

    return;
  }

  _selected_parameter_page = page_number;
}

void controllers::FaderfoxPc4::set_selected_channel_id(size_t channel_id) {
  logging::log<logging::LogLevel::Trace>(
      "FaderfoxPc4::set_selected_channel_id");

  if (channel_id > _channels.size() - 1) {
    logging::log<logging::LogLevel::Error>("Channel id {} out of bounds",
                                           channel_id);

    return;
  }

  _selected_channel_id = channel_id;
}

void controllers::FaderfoxPc4::set_selected_plugin_id(size_t plugin_id) {
  logging::log<logging::LogLevel::Trace>("FaderfoxPc4::set_selected_plugin_id");

  if (plugin_id > number_of_plugins - 1) {
    logging::log<logging::LogLevel::Error>("Plugin id {} out of bounds",
                                           plugin_id);

    return;
  }

  _selected_plugin_id = plugin_id;
}

void controllers::FaderfoxPc4::set_selected_channel_from_processor(
    const ChannelType channel_type, const size_t channel_id) {
  logging::log<logging::LogLevel::Trace>(
      "FaderfoxPc4::set_selected_channel_from_processor");

  if (channel_type == ChannelType::GROUP_CHANNEL) {
    set_selected_channel_id(channel_id + number_of_channels);
  } else {
    set_selected_channel_id(channel_id);
  }
}

void controllers::FaderfoxPc4::clear_selected_channel_from_processor() {
  logging::log<logging::LogLevel::Trace>(
      "FaderfoxPc4::clear_selected_channel_from_processor");
}

void controllers::FaderfoxPc4::set_selected_plugin_page_from_processor(
    const size_t plugin_id, const size_t page_number) {
  logging::log<logging::LogLevel::Trace>(
      "FaderfoxPc4::set_selected_plugin_page_from_processor");

  set_selected_plugin_id(plugin_id);
  set_selected_parameter_page(page_number);
}

void controllers::FaderfoxPc4::set_channel_node(ChannelType channel_type,
                                                size_t channel_id,
                                                uint64_t object_id) {
  logging::log<logging::LogLevel::Trace>("FaderfoxPc4::set_channel_node");
}

void controllers::FaderfoxPc4::set_channel_filter_node(ChannelType channel_type,
                                                       size_t channel_id,
                                                       uint64_t object_id) {
  logging::log<logging::LogLevel::Trace>(
      "FaderfoxPc4::set_channel_filter_node");

  size_t ff_channel_id{0};

  if (channel_type == ChannelType::GROUP_CHANNEL) {
    ff_channel_id = channel_id + number_of_channels;
  } else {
    ff_channel_id = channel_id;
  }

  if (ff_channel_id >= _channels.size()) {
    logging::log<logging::LogLevel::Error>("Channel id {} out of bounds",
                                           ff_channel_id);

    return;
  }

  const auto node = _registry.get_node_by_object_id(object_id);

  if (!node) {
    logging::log<logging::LogLevel::Error>("Couldn't find node {}", object_id);
    return;
  }

  _channels[ff_channel_id].filter_node = *node;
}

void controllers::FaderfoxPc4::set_channel_send_node(ChannelType channel_type,
                                                     size_t channel_id,
                                                     size_t send_id,
                                                     uint64_t object_id) {
  logging::log<logging::LogLevel::Trace>("FaderfoxPc4::set_channel_send_node");
}

void controllers::FaderfoxPc4::clear_filter_parameters(ChannelType channel_type,
                                                       size_t channel_id) {
  logging::log<logging::LogLevel::Trace>(
      "FaderfoxPc4::clear_filter_parameters");

  std::lock_guard lock(_channels_mutex);
  for (auto &channel : _channels) {
    for (auto &plugin : channel.plugins) {
      for (size_t page = 0; page < number_of_parameter_pages; page++) {
        for (size_t row = 0; row < number_of_rows; row++) {
          for (size_t column = 0; column < number_of_columns; column++) {
            plugin[page][row][column] = std::nullopt;
          }
        }
      }
    }
  }
}

void controllers::FaderfoxPc4::add_filter_parameter(
    const ChannelType channel_type, const size_t channel_id,
    const size_t plugin_id, char *name, const float min, const float max) {
  logging::log<logging::LogLevel::Trace>("FaderfoxPc4::add_filter_parameter");

  auto internal_channel = channel_id;
  if (channel_type == ChannelType::GROUP_CHANNEL) {
    internal_channel += number_of_channels;
  }

  if (internal_channel >= _channels.size()) {
    logging::log<logging::LogLevel::Error>(
        "Calculated internal channel {} out of bounds", internal_channel);

    return;
  }

  if (plugin_id >= number_of_plugins) {
    logging::log<logging::LogLevel::Error>("Plugin id {} out of bounds",
                                           plugin_id);

    return;
  }

  auto &plugin = _channels[channel_id].plugins[plugin_id];

  for (size_t page = 0; page < number_of_parameter_pages; page++) {
    for (size_t row = 0; row < number_of_rows; row++) {
      for (size_t column = 0; column < number_of_columns; column++) {
        if (plugin[page][row][column])
          continue;

        plugin[page][row][column] = Parameter{
            .name = name,
            .max = max,
            .min = min,
        };

        return;
      }
    }
  }
}
