#include "registry/Registry.h"

#include <algorithm>

std::optional<std::shared_ptr<registry::Node>>
registry::Registry::get_node_by_object_id(const uint64_t object_id) {
  auto lock = std::lock_guard(_nodes_mutex);

  auto node_it =
      std::ranges::lower_bound(_nodes, object_id, {}, &Node::object_id);

  if (node_it != _nodes.end() && (*node_it)->object_id() == object_id)
    return *node_it;

  const BindNodeData bind_node_data{
      .object_id = object_id,
  };

  pw_loop_invoke(
      pw_main_loop_get_loop(_loop),
      [](spa_loop *loop, bool async, std::uint32_t seq, const void *data,
         size_t size, void *user_data) {
        const auto bind_node_data_local =
            static_cast<const BindNodeData *>(data);
        const auto registry = static_cast<Registry *>(user_data);
        registry->_bind_node(bind_node_data_local->object_id);
        return 0;
      },
      0, &bind_node_data, sizeof(bind_node_data), true, this);

  node_it = std::ranges::lower_bound(_nodes, object_id, {}, &Node::object_id);

  if (node_it != _nodes.end() && (*node_it)->object_id() == object_id)
    return *node_it;

  return std::nullopt;
}

std::optional<std::shared_ptr<registry::Node>>
registry::Registry::_bind_node(const uint64_t object_id) {
  auto pw_node = static_cast<struct pw_node *>(pw_registry_bind(
      _registry, object_id, PW_TYPE_INTERFACE_Node, PW_VERSION_NODE, 0));

  if (pw_node == nullptr) {
    logging::log<logging::LogLevel::Error>(
        "Couldn't bind node with object id {}", object_id);
    return std::nullopt;
  }

  auto node = std::make_shared<registry::Node>(object_id, pw_node);
  _nodes.emplace_back(node);
  return node;
}
