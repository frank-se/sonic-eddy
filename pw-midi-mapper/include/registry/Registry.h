#pragma once

#include "registry/Node.h"

#include <memory>
#include <pipewire/pipewire.h>
#include <vector>

namespace registry {

class Registry {
public:
  Registry(pw_main_loop *loop, pw_core *core,
           controllers::MidiControlChangeUpdateCallback callback)
      : _loop(loop), _controller_update_callback(std::move(callback)) {
    _registry = pw_core_get_registry(core, PW_VERSION_REGISTRY, 0);
  }

  ~Registry() {
    if (_registry == nullptr)
      return;

    pw_proxy_destroy(reinterpret_cast<pw_proxy *>(_registry));
  }

  std::optional<Node *>
  get_node_by_object_id(uint64_t object_id,
                        controllers::ChannelType channel_type,
                        uint64_t channel_id);

  bool _bind_node(uint64_t object_id, controllers::ChannelType channel_type,
                  uint64_t channel_id);

private:
  pw_main_loop *_loop;
  pw_registry *_registry;
  controllers::MidiControlChangeUpdateCallback _controller_update_callback;

  std::mutex _nodes_mutex{};
  std::vector<std::unique_ptr<Node>> _nodes{};

  struct BindNodeData {
    uint64_t object_id;
    controllers::ChannelType channel_type;
    uint64_t channel_id;
  };
};

} // namespace registry
