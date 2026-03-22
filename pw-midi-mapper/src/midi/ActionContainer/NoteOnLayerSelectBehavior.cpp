#include "midi/ActionContainer/NoteOnLayerSelectBehavior.h"

void midi::action_container::NoteOnLayerSelectBehavior::process(
    const NoteOnMessages &message) {

  _layer_manager->activate_layer(_target_layer_id);
}
