#include "midi/ActionContainer.h"

#include "midi/ActionContainer/ControlChangeParamBehavior.h"
#include "midi/ActionContainer/NoteOnLayerSelectBehavior.h"
#include "midi/ActionContainer/SetVolumesBehavior.h"

#include <iostream>
#include <ranges>

size_t midi::ActionContainer::add_control_change_behaviour_set_volumes(
    size_t layer_id, const uint8_t group, const uint8_t channel,
    const uint8_t index, uint64_t object_id) {

  auto behavior = std::make_shared<action_container::SetVolumesBehavior>(
      object_id, _registry, _loop);

  pw_loop_invoke(
      pw_main_loop_get_loop(_loop),
      [](spa_loop *loop, bool async, std::uint32_t seq, const void *data,
         size_t size, void *user_data) {
        const auto behavior =
            static_cast<action_container::SetVolumesBehavior *>(user_data);
        behavior->setup_node_listener();
        return 0;
      },
      0, nullptr, 0, true, behavior.get());

  auto lock = std::lock_guard(_actions_mutex);

  _actions.emplace_back(action_container::ControlChangeMidiAction{
      .layer_id = layer_id,
      .group = group,
      .channel = channel,
      .index = index,
      .behavior = [behavior](const ControlChangeMessages &control_change) {
        behavior->process(control_change);
      }});

  return _actions.size() - 1;
}

size_t midi::ActionContainer::add_control_change_behavior_set_param(
    size_t layer_id, const uint8_t group, const uint8_t channel,
    const uint8_t index, uint64_t object_id, const std::string &param_name,
    float min, float max) {

  auto behavior =
      std::make_shared<action_container::ControlChangeParamBehavior>(
          object_id, _registry, _loop, param_name, min, max);

  pw_loop_invoke(
      pw_main_loop_get_loop(_loop),
      [](spa_loop *loop, bool async, std::uint32_t seq, const void *data,
         size_t size, void *user_data) {
        const auto behavior =
            static_cast<action_container::ControlChangeParamBehavior *>(
                user_data);
        behavior->setup_node_listener();
        return 0;
      },
      0, nullptr, 0, true, behavior.get());

  auto lock = std::lock_guard(_actions_mutex);

  _actions.emplace_back(action_container::ControlChangeMidiAction{
      .layer_id = layer_id,
      .group = group,
      .channel = channel,
      .index = index,
      .behavior = [behavior](const ControlChangeMessages &control_change) {
        behavior->process(control_change);
      }});

  return _actions.size() - 1;
}

size_t midi::ActionContainer::add_note_on_callback_behavior(
    size_t layer_id, const uint8_t group, const uint8_t channel,
    const uint8_t note_number, const action_container::NoteOnCallback &callback,
    size_t port_id) {

  auto lock = std::lock_guard(_actions_mutex);

  auto mapping_id = _actions.size();
  auto behavior = std::make_shared<action_container::NoteOnCallbackBehavior>(
      port_id, mapping_id, callback);

  _actions.emplace_back(action_container::NoteOnMidiAction{
      .layer_id = layer_id,
      .group = group,
      .channel = channel,
      .note_number = note_number,
      .behavior = [behavior](const NoteOnMessages &note_on) {
        behavior->process(note_on);
      }});

  return mapping_id;
}

size_t midi::ActionContainer::add_note_on_layer_select_behavior(
    const size_t layer_id, const uint8_t group, const uint8_t channel,
    const uint8_t note_number, const uint64_t target_layer_id) {

  auto behavior = std::make_shared<action_container::NoteOnLayerSelectBehavior>(
      _layer_manager, target_layer_id);

  auto lock = std::lock_guard(_actions_mutex);

  _actions.emplace_back(action_container::NoteOnMidiAction{
      .layer_id = layer_id,
      .group = group,
      .channel = channel,
      .note_number = note_number,
      .behavior = [behavior](const NoteOnMessages &note_on) {
        behavior->process(note_on);
      }});

  return _actions.size() - 1;
}

void midi::ActionContainer::process(const Message &message) {
  const auto old_layer = _layer_manager->active_layer();

  std::visit(
      [this](auto &&arg) {
        using T = std::decay_t<decltype(arg)>;
        if constexpr (std::is_same_v<T, midi::ControlChangeV2>) {
          process_control_change(arg);
        } else if constexpr (std::is_same_v<T, midi::NoteOnV2>) {
          process_note_on(arg);
        } else if constexpr (std::is_same_v<T, midi::NoteOffV2>) {
          process_note_off(arg);
        } else if constexpr (std::is_same_v<T, midi::ControlChangeV1>) {
          process_control_change(arg);
        } else if constexpr (std::is_same_v<T, midi::NoteOnV1>) {
          process_note_on(arg);
        } else if constexpr (std::is_same_v<T, midi::NoteOffV1>) {
          process_note_off(arg);
        }
      },
      message);

  if (_layer_manager->active_layer() != old_layer) {
    _feedback_manager->feedback_for_layer_id_change(
        _layer_manager->active_layer());
  }
}

void midi::ActionContainer::process_control_change(
    const ControlChangeMessages &message) {
  std::lock_guard lock(_actions_mutex);

  auto matching_actions =
      _actions | std::views::filter([&message, this](auto &&midi_action) {
        if (!std::holds_alternative<action_container::ControlChangeMidiAction>(
                midi_action))
          return false;

        auto control_change_action =
            std::get<action_container::ControlChangeMidiAction>(midi_action);

        if (!_layer_manager->is_active(control_change_action.layer_id))
          return false;

        uint8_t group = 0;
        uint8_t channel = 0;
        uint8_t index = 0;
        std::visit(
            [&group, &channel, &index](auto &&arg) {
              using T = std::decay_t<decltype(arg)>;
              if constexpr (std::is_same_v<T, ControlChangeV2>) {
                group = arg.group;
                channel = arg.channel;
                index = arg.index;
              } else if constexpr (std::is_same_v<T, ControlChangeV1>) {
                channel = arg.channel;
                index = arg.index;
              }
            },
            message);

        return control_change_action.group == group &&
               control_change_action.channel == channel &&
               control_change_action.index == index;
      });

  for (auto &&action : matching_actions) {
    std::get<action_container::ControlChangeMidiAction>(action).behavior(
        message);
  }
}

void midi::ActionContainer::process_note_on(const NoteOnMessages &message) {
  std::lock_guard lock(_actions_mutex);

  auto matching_actions =
      _actions | std::views::filter([&message, this](auto &&midi_action) {
        if (!std::holds_alternative<action_container::NoteOnMidiAction>(
                midi_action))
          return false;

        auto note_on_action =
            std::get<action_container::NoteOnMidiAction>(midi_action);

        if (!_layer_manager->is_active(note_on_action.layer_id))
          return false;

        uint8_t group = 0;
        uint8_t channel = 0;
        uint8_t note_number = 0;

        std::visit(
            [&group, &channel, &note_number](auto &&arg) {
              using T = std::decay_t<decltype(arg)>;
              if constexpr (std::is_same_v<T, NoteOnV2>) {
                group = arg.group;
                channel = arg.channel;
                note_number = arg.note_number;
              } else if constexpr (std::is_same_v<T, NoteOnV1>) {
                channel = arg.channel;
                note_number = arg.note_number;
              }
            },
            message);

        return note_on_action.group == group &&
               note_on_action.channel == channel &&
               note_on_action.note_number == note_number;
      });

  for (auto &&action : matching_actions) {
    std::get<action_container::NoteOnMidiAction>(action).behavior(message);
  }
}

void midi::ActionContainer::process_note_off(const NoteOffMessages &message) {
  std::lock_guard lock(_actions_mutex);

  auto matching_actions =
      _actions | std::views::filter([&message, this](auto &&midi_action) {
        if (!std::holds_alternative<action_container::NoteOffMidiAction>(
                midi_action))
          return false;

        auto note_off_action =
            std::get<action_container::NoteOffMidiAction>(midi_action);

        if (!_layer_manager->is_active(note_off_action.layer_id))
          return false;

        uint8_t group = 0;
        uint8_t channel = 0;
        uint8_t note_number = 0;

        std::visit(
            [&group, &channel, &note_number](auto &&arg) {
              using T = std::decay_t<decltype(arg)>;
              if constexpr (std::is_same_v<T, NoteOffV2>) {
                group = arg.group;
                channel = arg.channel;
                note_number = arg.note_number;
              } else if constexpr (std::is_same_v<T, NoteOffV1>) {
                channel = arg.channel;
                note_number = arg.note_number;
              }
            },
            message);

        return note_off_action.group == group &&
               note_off_action.channel == channel &&
               note_off_action.note_number == note_number;
      });

  for (auto &&action : matching_actions) {
    std::get<action_container::NoteOffMidiAction>(action).behavior(message);
  }
}
