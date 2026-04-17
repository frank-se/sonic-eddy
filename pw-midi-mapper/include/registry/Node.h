#pragma once

#include "audio/pan.h"
#include "controllers/Callbacks.h"
#include "controllers/CatchUpHandler.h"
#include "controllers/Parameter.h"
#include "pw_utils/set_pw_node_param.h"

#include <atomic>
#include <chrono>

namespace registry {

class Node {
public:
  explicit Node(pw_main_loop *loop, const uint64_t object_id, pw_node *node,
                controllers::MidiControlChangeUpdateCallback callback,
                const controllers::ChannelType channel_type,
                const uint64_t channel_id)
      : _loop(loop), _object_id(object_id), _node(node),
        _controller_update_callback(std::move(callback)),
        _channel_type(channel_type), _channel_id(channel_id),
        _volume_parameter_name("channel_volumes") {

    subscribe_to_param_updates();
  }

  ~Node() {
    if (_node == nullptr)
      return;

    pw_proxy_destroy(reinterpret_cast<pw_proxy *>(_node));
    _node = nullptr;
  }

  [[nodiscard]] uint64_t object_id() const { return _object_id; }

  void set_channel_volumes(std::array<float, 2> volumes);
  void set_volume(double value);

  [[nodiscard]] std::optional<std::array<float, 2>> channel_volumes() const;
  [[nodiscard]] std::optional<audio::pan::PanAndVolume> pan_and_volume() const;

  void set_param(const controllers::Parameter & parameter, float normalized_controller_value);
  void set_param(const std::string &name, float value) const;

  std::optional<float> parameter_value(const std::string &name);

private:
  pw_main_loop *_loop;
  uint64_t _object_id;
  pw_node *_node;
  controllers::MidiControlChangeUpdateCallback _controller_update_callback;
  controllers::ChannelType _channel_type;
  size_t _channel_id;

  std::atomic<bool> _subscribed_to_param_updates = false;

  std::atomic<float> _left = 0.0f;
  std::atomic<float> _right = 0.0f;

  controllers::CatchUpHandler _volume_catch_up_handler{};
  std::string _volume_parameter_name;

  spa_hook _node_listener{};

  std::mutex _param_values_mutex{};
  std::unordered_map<std::string, float> _param_values{};

  static void on_node_params_changed(void *user_data, int sequence_number,
                                     uint32_t id, uint32_t index, uint32_t next,
                                     const spa_pod *pod);

  void subscribe_to_param_updates();

  static constexpr pw_node_events _node_events = {
      .version = PW_VERSION_NODE_EVENTS, .param = on_node_params_changed};
};

} // namespace registry
