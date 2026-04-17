#pragma once

#include "DialMode.h"
#include "Parameter.h"
#include "pipewire/pipewire.h"
#include "registry/Node.h"

#include <array>
#include <atomic>
#include <optional>

namespace controllers {

class Channel {
public:
  explicit Channel(const ChannelType channel_type, const size_t channel_id,
                   MidiControlChangeUpdateCallback callback)
      : _channel_type(channel_type), _channel_id(channel_id),
        _controller_update_callback(std::move(callback)) {}

  ~Channel() = default;

  void swap_dial_mode();
  [[nodiscard]] DialMode dial_mode() const;

  void increment_selected_filter_param_section();
  [[nodiscard]] uint8_t selected_filter_params_section() const;

  void
  set_parameter_for_selected_section(size_t parameter_id,
                                     double normalized_controller_value) const;

  void set_volume(double normalized_controller_value) const;

  void set_send_trim(size_t send_id, double value) const;

  void set_playback_node(registry::Node *node) { _playback_node = node; }

  void set_filter_node(registry::Node *node) {
    _filter_node = node;
  }

  void set_send_node(size_t send_id, registry::Node *node);

  void clear_parameters();

  void add_parameter(size_t plugin_id, char *name, float min, float max);

private:
  ChannelType _channel_type;
  size_t _channel_id;
  MidiControlChangeUpdateCallback _controller_update_callback;

  registry::Node *_playback_node = nullptr;
  std::array<registry::Node *, 4> _sends = {nullptr, nullptr, nullptr, nullptr};
  registry::Node *_filter_node = nullptr;

  std::atomic<uint8_t> _selected_filter_params_section{0};
  std::atomic<DialMode> _dial_mode{SENDS};

  std::array<std::array<std::optional<Parameter>, 4>, 3> _plugins{};
};

} // namespace controllers
