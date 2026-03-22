#pragma once
#include <atomic>
#include <limits>
#include <memory>

namespace layers {

constexpr size_t ACTIVE_ON_ALL_LAYERS = std::numeric_limits<size_t>::max();

class LayerManager {
public:
  void activate_layer(const size_t layer_id) { _active_layer = layer_id; }

  size_t active_layer() { return _active_layer; }

  bool is_active(const size_t layer_id) const {
    return layer_id == _active_layer || layer_id == ACTIVE_ON_ALL_LAYERS;
  };

private:
  std::atomic<size_t> _active_layer{};
};

using LayerManagerPtr = std::shared_ptr<LayerManager>;

} // namespace layers
