#pragma once
#include <utility>

#include "layers/LayerManager.h"
#include "midi/Messages.h"

namespace midi::action_container {

class NoteOnLayerSelectBehavior {
public:
  NoteOnLayerSelectBehavior(layers::LayerManagerPtr layer_manager,
                            const size_t target_layer_id)
      : _layer_manager(std::move(layer_manager)),
        _target_layer_id(target_layer_id) {}

  void process(const NoteOnMessages &message);

private:
  layers::LayerManagerPtr _layer_manager;
  size_t _target_layer_id;
};

} // namespace midi::action_container