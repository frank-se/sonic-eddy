#pragma once

#include "IController.h"
#include "controllers/Parameter.h"
#include "midi/Sender.h"
#include "Registry.h"

#include <pipewire/pipewire.h>

namespace controllers {

constexpr size_t number_of_plugins = 5;
constexpr size_t number_of_parameter_pages = 5;
constexpr size_t number_of_rows = 6;
constexpr size_t number_of_columns = 4;
constexpr size_t parameters_per_plugin =
    number_of_rows * number_of_columns * number_of_parameter_pages;

constexpr size_t number_of_channels = 16;

using ParameterRow = std::array<std::optional<Parameter>, number_of_columns>;
using ParameterPage = std::array<ParameterRow, number_of_rows>;
using Plugin = std::array<ParameterPage, number_of_parameter_pages>;
using Plugins = std::array<Plugin, number_of_plugins>;

struct ChannelPlugin {
  registry::Node *filter_node = nullptr;
  Plugins plugins{};
};

using ChannelPlugins = std::array<ChannelPlugin, 24>;

class FaderfoxPc4 : public IController {
public:
  FaderfoxPc4(midi::Registry &registry, pw_loop *loop)
      : _registry(registry), _loop(loop) {}

  void process(const midi::Message &message) override;
  void handle_normalized_control_change(size_t index, double value);

  void set_layer_from_processor(size_t layer) override;

  void set_selected_channel_from_processor(ChannelType channel_type,
                                           size_t channel_id) override;

  void clear_selected_channel_from_processor() override;

  void set_selected_plugin_page_from_processor(size_t plugin_id,
                                               size_t page_number) override;

  void set_parameter(size_t channel_id, size_t plugin_id, size_t parameter_id,
                     std::string &name, float max, float min);

  void set_filter_node(size_t channel_id, registry::Node *node);

  void add_current_state_as_feedback() override {};

  void set_master_channel_node(size_t layer_id, uint64_t object_id) override {};
  void set_channel_node(ChannelType channel_type, size_t channel_id,
                        uint64_t object_id) override;
  void set_channel_filter_node(ChannelType channel_type, size_t channel_id,
                               uint64_t object_id) override;
  void set_channel_send_node(ChannelType channel_type, size_t channel_id,
                             size_t send_id, uint64_t object_id) override;
  void clear_filter_parameters(ChannelType channel_type,
                               size_t channel_id) override;
  void add_filter_parameter(ChannelType channel_type, size_t channel_id,
                            size_t plugin_id, char *name, float min,
                            float max) override;

private:
  midi::Registry &_registry;
  pw_loop *_loop;

  std::mutex _channels_mutex;
  ChannelPlugins _channels{};

  std::atomic<size_t> _selected_channel_id{0};
  std::atomic<size_t> _selected_plugin_id{0};
  std::atomic<size_t> _selected_parameter_page{0};

  void set_selected_channel_id(size_t channel_id);
  void set_selected_plugin_id(size_t plugin_id);
  void set_selected_parameter_page(size_t page_number);
};

} // namespace controllers
