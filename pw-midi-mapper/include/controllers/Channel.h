#pragma once

#include "pipewire/pipewire.h"

#include <array>
#include <atomic>
#include <optional>
#include <string>

namespace controllers {

enum DialMode { SENDS = 1, FILTER_PARAMS = 2 };

struct NodeData {
  std::optional<uint32_t> object_id;
  pw_node *node = nullptr;
  spa_hook node_listener{};
};

struct Parameter {
  std::string name;
  float value;
  float max;
  float min;
};

struct Channel {
  NodeData channel_playback_node;
  std::array<NodeData, 4> send_channels;
  NodeData channel_filter_node;

  std::atomic<float> volume;
  std::atomic<float> pan;

  std::atomic<uint8_t> selected_filter_params_section{0};
  std::atomic<DialMode> dials_mode;

  std::array<std::array<std::optional<Parameter>, 3>, 3> parameters{};
};

} // namespace controllers
