#pragma once

#include "registry/Node.h"

#include <memory>
#include <pipewire/pipewire.h>
#include <vector>

namespace registry {

class Registry {
public:
  Registry(pw_main_loop *loop, pw_core *core) : _loop(loop) {
    _registry = pw_core_get_registry(core, PW_VERSION_REGISTRY, 0);
  }

  ~Registry() {
    if (_registry == nullptr)
      return;

    pw_proxy_destroy(reinterpret_cast<pw_proxy *>(_registry));
  }

  std::optional<Node *> get_node_by_object_id(uint64_t object_id);

  void _bind_node(uint64_t object_id);

private:
  pw_main_loop *_loop;
  pw_registry *_registry;

  std::mutex _nodes_mutex{};
  std::vector<std::unique_ptr<Node>> _nodes{};

  struct BindNodeData {
    uint64_t object_id;
  };
};

} // namespace registry
