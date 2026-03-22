#pragma once
#include "audio/pan.h"
#include "pw_utils/set_pw_node_param.h"

#include <atomic>

namespace registry {

class Node {
public:
  explicit Node(pw_main_loop *loop, const uint64_t object_id, pw_node *node)
      : _loop(loop), _object_id(object_id), _node(node) {

    subscribe_to_param_updates();
  }

  ~Node() {
    if (_node == nullptr)
      return;

    pw_proxy_destroy(reinterpret_cast<pw_proxy *>(_node));
    _node = nullptr;
  }

  [[nodiscard]] uint64_t object_id() const { return _object_id; }

  void set_channel_volumes(std::array<float, 2> volumes) const;
  void set_volume(double value) const;

  [[nodiscard]] std::optional<std::array<float, 2>> channel_volumes() const;
  [[nodiscard]] std::optional<audio::pan::PanAndVolume> pan_and_volume() const;

  void set_param(const std::string &name, float value) const;

private:
  pw_main_loop *_loop;
  uint64_t _object_id;
  pw_node *_node;

  std::atomic<bool> _subscribed_to_param_updates = false;

  std::atomic<float> _left = 0.0f;
  std::atomic<float> _right = 0.0f;

  spa_hook _node_listener{};

  static void
  on_channel_playback_node_params_changed(void *user_data, int sequence_number,
                                          uint32_t id, uint32_t index,
                                          uint32_t next, const spa_pod *pod);

  void subscribe_to_param_updates();

  static constexpr pw_node_events _node_events = {
      .version = PW_VERSION_NODE_EVENTS,
      .param = on_channel_playback_node_params_changed};
};

} // namespace registry
