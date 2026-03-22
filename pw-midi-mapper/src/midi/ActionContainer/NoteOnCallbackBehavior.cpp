#include "midi/ActionContainer/NoteOnCallbackBehavior.h"

void midi::action_container::NoteOnCallbackBehavior::process(
    const NoteOnMessages &control_change) {
  std::visit(
      [this](auto &&message) {
        using T = std::decay_t<decltype(message)>;
        if constexpr (std::is_same_v<T, NoteOnV1>) {
          _callback(_port_id, _mapping_id, message.note_number,
                    message.velocity);
        } else if constexpr (std::is_same_v<T, NoteOnV2>) {
          _callback(_port_id, _mapping_id, message.note_number,
                    message.velocity);
        }
      },
      control_change);
}
