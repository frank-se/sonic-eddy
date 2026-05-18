#include "Registry.h"

#include "logging/log.h"

#include <algorithm>
#include <mutex>

std::optional<registry::Node *> midi::Registry::get_node_by_object_id(
    const uint64_t object_id, const controllers::ChannelType channel_type,
    const uint64_t channel_id) {
  logging::log<logging::LogLevel::Trace>("Registry::get_node_by_object_id");

  auto lock = std::lock_guard(_nodes_mutex);

  auto node_it =
      std::ranges::lower_bound(_nodes, object_id, {}, &registry::Node::object_id);

  if (node_it != _nodes.end() && (*node_it)->object_id() == object_id)
    return node_it->get();

  const BindNodeData bind_node_data{.object_id   = object_id,
                                    .channel_type = channel_type,
                                    .channel_id   = channel_id};

  /* _loop is already pw_loop* */
  const auto result = pw_loop_invoke(
      _loop,
      [](spa_loop *loop, bool async, std::uint32_t seq, const void *data,
         size_t size, void *user_data) {
        const auto bind_data = static_cast<const BindNodeData *>(data);
        const auto registry  = static_cast<midi::Registry *>(user_data);
        return registry->_bind_node(bind_data->object_id,
                                    bind_data->channel_type,
                                    bind_data->channel_id)
                   ? 0
                   : -1;
      },
      0, &bind_node_data, sizeof(bind_node_data), true, this);

  if (result == -1) {
    logging::log<logging::LogLevel::Error>(
        "Couldn't bind node with object id {}", object_id);
    return std::nullopt;
  }

  node_it =
      std::ranges::lower_bound(_nodes, object_id, {}, &registry::Node::object_id);

  if (node_it != _nodes.end() && (*node_it)->object_id() == object_id)
    return node_it->get();

  return std::nullopt;
}

bool midi::Registry::_bind_node(const uint64_t object_id,
                                const controllers::ChannelType channel_type,
                                const uint64_t channel_id) {
  logging::log<logging::LogLevel::Trace>("Registry::_bind_node");

  auto pw_node = static_cast<struct pw_node *>(pw_registry_bind(
      _registry, object_id, PW_TYPE_INTERFACE_Node, PW_VERSION_NODE, 0));

  if (pw_node == nullptr) {
    logging::log<logging::LogLevel::Error>(
        "Couldn't bind node with object id {}", object_id);
    return false;
  }

  logging::log<logging::LogLevel::Debug>("Bound node with object id {}",
                                         object_id);
  _nodes.emplace_back(std::make_unique<registry::Node>(
      _loop, object_id, pw_node, _controller_update_callback,
      channel_type, channel_id));

  std::ranges::sort(_nodes, {}, &registry::Node::object_id);
  return true;
}
