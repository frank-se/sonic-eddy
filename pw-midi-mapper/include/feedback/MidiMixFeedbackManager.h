#include "feedback/IFeedbankManager.h"
#include "midi/Sender.h"

namespace feedback {

class MidiMixFeedbackManager : public IFeedbackManager {
public:
  explicit MidiMixFeedbackManager(midi::SenderPointer sender)
      : _sender(std::move(sender)) {};

  void initial_state() override;
  void feedback_for_layer_id_change(size_t layer_id) override;

private:
  midi::SenderPointer _sender;
  uint64_t _last_layer_id{0};

  void add_midi_messages(uint64_t layer_id) const;
};

} // namespace feedback