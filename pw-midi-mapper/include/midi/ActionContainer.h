#pragma once

#include "ActionContainer/MidiActions.h"
#include "ActionContainer/NoteOnCallbackBehavior.h"
#include "action_container/IActionContainer.h"
#include "feedback/IFeedbankManager.h"
#include "layers/LayerManager.h"

#include <pipewire/pipewire.h>

#include <memory>
#include <mutex>
#include <utility>
#include <vector>

namespace midi {

class ActionContainer : public ::action_container::IActionContainer {
public:
  ActionContainer(pw_main_loop *loop, pw_registry *registry,
                  layers::LayerManagerPtr layer_manager,
                  feedback::FeedbackManagerPtr feedback_manager)
      : _layer_manager(std::move(layer_manager)),
        _feedback_manager(std::move(feedback_manager)), _loop(loop),
        _registry(registry) {}

  size_t add_control_change_behaviour_set_volumes(size_t layer_id,
                                                  uint8_t group,
                                                  uint8_t channel,
                                                  uint8_t index,
                                                  uint64_t object_id);

  size_t add_control_change_behavior_set_param(size_t layer_id, uint8_t group,
                                               uint8_t channel, uint8_t index,
                                               uint64_t object_id,
                                               const std::string &param_name,
                                               float min, float max);

  size_t add_note_on_callback_behavior(
      size_t layer_id, uint8_t group, uint8_t channel, uint8_t note_number,
      const action_container::NoteOnCallback &callback, size_t port_id);

  size_t add_note_on_layer_select_behavior(size_t layer_id, uint8_t group,
                                           uint8_t channel, uint8_t note_number,
                                           uint64_t target_layer_id);

  void process(const Message &message);

private:
  layers::LayerManagerPtr _layer_manager;
  std::pmr::vector<action_container::MidiAction> _actions;
  feedback::FeedbackManagerPtr _feedback_manager;

  std::mutex _actions_mutex;

  pw_main_loop *_loop = nullptr;
  pw_registry *_registry;

  void process_control_change(const ControlChangeMessages &message);
  void process_note_on(const NoteOnMessages &message);
  void process_note_off(const NoteOffMessages &message);
};

} // namespace midi
