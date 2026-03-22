#include "feedback/MidiMixFeedbackManager.h"

void feedback::MidiMixFeedbackManager::initial_state() {
  add_midi_messages(0);
  for (int i = 0; i < 8; i++) {
    _sender->push(midi::NoteOnV1{
        .channel = 0,
        .note_number = static_cast<uint8_t>(2 + (3 * i)),
        .velocity = 127,
    });
    _sender->push(midi::NoteOnV1{
        .channel = 0,
        .note_number = static_cast<uint8_t>(3 + (3 * i)),
        .velocity = 127,
    });
  }
}

void feedback::MidiMixFeedbackManager::feedback_for_layer_id_change(
    const size_t layer_id) {

  if (layer_id == _last_layer_id)
    return;

  add_midi_messages(layer_id);

  _last_layer_id = layer_id;
}

void feedback::MidiMixFeedbackManager::add_midi_messages(
    const uint64_t layer_id) const {
  if (layer_id == 0) {
    /*
    _sender->push(midi::NoteOffV2{
        .group = 0,
        .channel = 0,
        .note_number = 25,
        .velocity = 0,
    });
    _sender->push(midi::NoteOnV2{
        .group = 0,
        .channel = 0,
        .note_number = 26,
        .velocity = std::numeric_limits<uint16_t>::max(),
    });
    */
    _sender->push(midi::NoteOnV1{
        .channel = 0,
        .note_number = 25,
        .velocity = 127,
    });
    _sender->push(midi::NoteOnV1{
        .channel = 0,
        .note_number = 26,
        .velocity = 0,
    });
  } else if (layer_id == 1) {
    /*
    _sender->push(midi::NoteOnV2{
        .group = 0,
        .channel = 0,
        .note_number = 25,
        .velocity = std::numeric_limits<uint16_t>::max(),
    });
    _sender->push(midi::NoteOffV2{
        .group = 0,
        .channel = 0,
        .note_number = 26,
        .velocity = 0,
    });
    */
    _sender->push(midi::NoteOnV1{
        .channel = 0,
        .note_number = 25,
        .velocity = 0,
    });
    _sender->push(midi::NoteOnV1{
        .channel = 0,
        .note_number = 26,
        .velocity = 127,
    });
  } else {
    /*
    _sender->push(midi::NoteOffV2{
        .group = 0,
        .channel = 0,
        .note_number = 25,
        .velocity = 0,
    });
    _sender->push(midi::NoteOffV2{
        .group = 0,
        .channel = 0,
        .note_number = 26,
        .velocity = 0,
    });
    */
    _sender->push(midi::NoteOnV1{
        .channel = 0,
        .note_number = 25,
        .velocity = 0,
    });
    _sender->push(midi::NoteOnV1{
        .channel = 0,
        .note_number = 26,
        .velocity = 0,
    });
  }
}
