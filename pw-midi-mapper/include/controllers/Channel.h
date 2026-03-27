#pragma once

#include "Parameter.h"
#include "pipewire/pipewire.h"
#include "registry/Node.h"

#include <array>
#include <atomic>
#include <optional>

namespace controllers {

enum DialMode { SENDS = 1, FILTER_PARAMS = 2 };

class Channel {
public:
  explicit Channel(const size_t channel_id) : _channel_id(channel_id) {}

  ~Channel() = default;

  void swap_dial_mode();
  [[nodiscard]] DialMode dial_mode() const;

  void increment_selected_filter_param_section();
  [[nodiscard]] uint8_t selected_filter_params_section() const;

  void set_parameter_for_selected_section(size_t parameter_id,
                                          double value) const;

  void set_volume(double value) const;

  void set_send_trim(size_t send_id, double value) const;

  void set_playback_node(registry::Node *node) { _playback_node = node; }

  void set_filter_node(registry::Node *node) { _filter_node = node; }

  void set_send_node(size_t send_id, registry::Node *node);

  void clear_parameters();

  void add_parameter(size_t plugin_id, char *name, float min, float max);

private:
  size_t _channel_id;

  registry::Node *_playback_node = nullptr;
  std::array<registry::Node *, 4> _sends = {nullptr, nullptr, nullptr, nullptr};
  registry::Node *_filter_node = nullptr;

  std::atomic<uint8_t> _selected_filter_params_section{0};
  std::atomic<DialMode> _dial_mode{SENDS};

  std::array<std::array<std::optional<Parameter>, 4>, 3> _plugins{};
};

} // namespace controllers
