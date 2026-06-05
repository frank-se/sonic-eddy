#include "Processor.h"
#include "controllers/CmdMm1.h"
#include "controllers/FaderfoxPc4.h"
#include "controllers/LaunchpadMini.h"
#include "logging/log.h"

/* ── lifecycle ───────────────────────────────────────────────────────────── */

void midi::Processor::start() {
  logging::log<logging::LogLevel::Trace>("Processor::start");
  start_processing_thread();
}

void midi::Processor::stop() {
  logging::log<logging::LogLevel::Trace>("Processor::stop");
  _is_running = false;
  _queue_wait_condition.notify_all();
  _midi_processing_thread.join();
}

void midi::Processor::start_processing_thread() {
  logging::log<logging::LogLevel::Trace>("Processor::start_processing_thread");

  _midi_processing_thread = std::thread([this]() {
    while (_is_running) {
      while (process_queues()) {
        /* drain until all queues empty */
      }
      auto lock = std::unique_lock(_queue_wait_mutex);
      _queue_wait_condition.wait_for(lock, std::chrono::seconds(1));
    }
  });
}

/* ── port creation ───────────────────────────────────────────────────────── */

static int setup_receiver_function(spa_loop *loop, bool async, uint32_t seq,
                                   const void *data, size_t size,
                                   void *user_data) {
  static_cast<midi::Receiver *>(user_data)->setup();
  return 0;
}

static int setup_sender_function(spa_loop *loop, bool async, uint32_t seq,
                                 const void *data, size_t size,
                                 void *user_data) {
  static_cast<midi::Sender *>(user_data)->setup();
  return 0;
}

std::optional<size_t> midi::Processor::create_midi_mix_port(
    const char *pmx_purpose, const char *pmx_tag,
    const LayerSelectCallback &layer_select_callback,
    const ChannelSelectCallback &channel_select_callback,
    const DialSectionModeCallback &dial_section_mode_select_callback,
    const FilterParamsSectionSelectCallback &filter_params_section_select_callback) {
  logging::log<logging::LogLevel::Trace>("Processor::create_midi_mix_port");

  std::lock_guard controllers_lock(_controllers_mutex);
  const auto port_id = _controllers.size();

  const auto receiver_name = std::format("midi-receiver {} Midi Mix", port_id);
  const auto sender_name   = std::format("midi-sender {} Midi Mix", port_id);

  const auto receiver = std::make_shared<Receiver>(
      pmx_purpose, pmx_tag, receiver_name, _loop, MidiVersion::UMP,
      &_queue_wait_mutex, &_queue_wait_condition);
  pw_loop_invoke(_loop, setup_receiver_function, SPA_ID_INVALID,
                 nullptr, 0, false, receiver.get());

  const auto sender = std::make_shared<Sender>(pmx_purpose, pmx_tag,
                                               sender_name, _loop);
  pw_loop_invoke(_loop, setup_sender_function, SPA_ID_INVALID,
                 nullptr, 0, false, sender.get());

  const auto midi_mix = std::make_shared<controllers::MidiMix>(
      *_registry, _loop, sender,
      [this, layer_select_callback](const size_t layer_id) {
        for (const auto &c : _controllers)
          c->set_layer_from_processor(layer_id);
        layer_select_callback(layer_id);
      },
      [this, channel_select_callback](const size_t channel_id) {
        for (const auto &c : _controllers)
          c->set_selected_channel_from_processor(
              controllers::ChannelType::CHANNEL, channel_id);
        channel_select_callback(controllers::ChannelType::CHANNEL, channel_id);
      },
      [dial_section_mode_select_callback](const size_t channel_id,
                                          const controllers::DialMode mode) {
        dial_section_mode_select_callback(controllers::ChannelType::CHANNEL,
                                          channel_id, mode);
      },
      [filter_params_section_select_callback](const size_t channel_id,
                                              const size_t section_id) {
        filter_params_section_select_callback(controllers::ChannelType::CHANNEL,
                                              channel_id, section_id);
      },
      _controller_update_callback);

  _controllers.push_back(midi_mix);
  _receiver_bindings.push_back({.receiver = receiver, .controller = midi_mix});
  midi_mix->add_current_state_as_feedback();
  return port_id;
}

std::optional<size_t> midi::Processor::create_mm_1_port(
    const char *pmx_purpose, const char *pmx_tag,
    const LayerSelectCallback &layer_select_callback,
    const ChannelSelectCallback &channel_select_callback,
    const DialSectionModeCallback &dial_section_mode_select_callback,
    const FilterParamsSectionSelectCallback &filter_params_section_select_callback,
    const FilterParamsSectionMovePagesRightCallback &pages_right_callback,
    const FilterParamsSectionMovePagesLeftCallback  &pages_left_callback) {
  logging::log<logging::LogLevel::Trace>("Processor::create_mm_1_port");

  std::lock_guard controllers_lock(_controllers_mutex);
  const auto port_id = _controllers.size();

  const auto receiver_name = std::format("midi-receiver {} MM1", port_id);
  const auto sender_name   = std::format("midi-sender {} MM1", port_id);

  const auto receiver = std::make_shared<Receiver>(
      pmx_purpose, pmx_tag, receiver_name, _loop, MidiVersion::UMP,
      &_queue_wait_mutex, &_queue_wait_condition);
  pw_loop_invoke(_loop, setup_receiver_function, SPA_ID_INVALID,
                 nullptr, 0, false, receiver.get());

  const auto sender = std::make_shared<Sender>(pmx_purpose, pmx_tag,
                                               sender_name, _loop);
  pw_loop_invoke(_loop, setup_sender_function, SPA_ID_INVALID,
                 nullptr, 0, false, sender.get());

  const auto cmd_mm_1 = std::make_shared<controllers::CmdMm1>(
      *_registry, _loop, sender,
      [this, layer_select_callback](const size_t layer_id) {
        for (const auto &c : _controllers)
          c->set_layer_from_processor(layer_id);
        layer_select_callback(layer_id);
      },
      [this, channel_select_callback](const size_t channel_id) {
        for (const auto &c : _controllers)
          c->set_selected_channel_from_processor(
              controllers::ChannelType::GROUP_CHANNEL, channel_id);
        channel_select_callback(controllers::ChannelType::GROUP_CHANNEL, channel_id);
      },
      [dial_section_mode_select_callback](const size_t channel_id,
                                          const controllers::DialMode dial_mode) {
        dial_section_mode_select_callback(controllers::ChannelType::GROUP_CHANNEL,
                                          channel_id, dial_mode);
      },
      [filter_params_section_select_callback](const size_t channel_id,
                                              const size_t section_id) {
        filter_params_section_select_callback(
            controllers::ChannelType::GROUP_CHANNEL, channel_id, section_id);
      },
      [pages_right_callback](const uint64_t step_count) {
        pages_right_callback(step_count);
      },
      [pages_left_callback](const uint64_t step_count) {
        pages_left_callback(step_count);
      },
      _controller_update_callback);

  _controllers.push_back(cmd_mm_1);
  _receiver_bindings.push_back(
      {.receiver = receiver, .controller = cmd_mm_1});
  cmd_mm_1->add_current_state_as_feedback();
  return port_id;
}

std::optional<size_t> midi::Processor::create_fader_fox_pc4_port(
    const char *pmx_purpose, const char *pmx_tag) {
  logging::log<logging::LogLevel::Trace>("Processor::create_fader_fox_pc4_port");

  std::lock_guard controllers_lock(_controllers_mutex);
  const auto port_id = _controllers.size();

  const auto receiver_name = std::format("midi-receiver {} FF PC4", port_id);

  const auto receiver = std::make_shared<Receiver>(
      pmx_purpose, pmx_tag, receiver_name, _loop, MidiVersion::UMP,
      &_queue_wait_mutex, &_queue_wait_condition);
  pw_loop_invoke(_loop, setup_receiver_function, SPA_ID_INVALID,
                 nullptr, 0, false, receiver.get());

  const auto faderfox_pc4 = std::make_shared<controllers::FaderfoxPc4>(
      *_registry, _loop);

  _controllers.push_back(faderfox_pc4);
  _receiver_bindings.push_back(
      {.receiver = receiver, .controller = faderfox_pc4});
  return port_id;
}

std::optional<size_t> midi::Processor::create_launchpad_mini_port(
    const char *pmx_purpose, const char *pmx_tag,
    const LayerSelectCallback &layer_select_callback,
    const controllers::LaunchpadMiniLoopPressedCallback &loop_pressed_callback,
    const controllers::LaunchpadMiniFaderChangedCallback
        &fader_changed_callback) {
  logging::log<logging::LogLevel::Trace>(
      "Processor::create_launchpad_mini_port");

  std::lock_guard controllers_lock(_controllers_mutex);
  const auto port_id = _controllers.size();

  const auto mi_receiver_name =
      std::format("midi-receiver {} Launchpad Mini MI", port_id);
  const auto da_receiver_name =
      std::format("midi-receiver {} Launchpad Mini DA", port_id);
  const auto mi_sender_name =
      std::format("midi-sender {} Launchpad Mini MI", port_id);
  const auto da_sender_name =
      std::format("midi-sender {} Launchpad Mini DA", port_id);

  const auto mi_receiver = std::make_shared<Receiver>(
      pmx_purpose, pmx_tag, mi_receiver_name, _loop, MidiVersion::UMP,
      &_queue_wait_mutex, &_queue_wait_condition);
  pw_loop_invoke(_loop, setup_receiver_function, SPA_ID_INVALID, nullptr, 0,
                 false, mi_receiver.get());

  const auto da_receiver = std::make_shared<Receiver>(
      pmx_purpose, pmx_tag, da_receiver_name, _loop, MidiVersion::UMP,
      &_queue_wait_mutex, &_queue_wait_condition);
  pw_loop_invoke(_loop, setup_receiver_function, SPA_ID_INVALID, nullptr, 0,
                 false, da_receiver.get());

  const auto mi_sender =
      std::make_shared<Sender>(pmx_purpose, pmx_tag, mi_sender_name, _loop);
  pw_loop_invoke(_loop, setup_sender_function, SPA_ID_INVALID, nullptr, 0,
                 false, mi_sender.get());

  const auto da_sender =
      std::make_shared<Sender>(pmx_purpose, pmx_tag, da_sender_name, _loop);
  pw_loop_invoke(_loop, setup_sender_function, SPA_ID_INVALID, nullptr, 0,
                 false, da_sender.get());

  const auto launchpad = std::make_shared<controllers::LaunchpadMini>(
      mi_sender, da_sender,
      [this, layer_select_callback](const size_t layer_id) {
        for (const auto &c : _controllers)
          c->set_layer_from_processor(layer_id);
        layer_select_callback(layer_id);
      },
      loop_pressed_callback, fader_changed_callback);

  _controllers.push_back(launchpad);
  _receiver_bindings.push_back(
      {.receiver = mi_receiver, .controller = launchpad});
  _receiver_bindings.push_back(
      {.receiver = da_receiver, .controller = launchpad});
  launchpad->add_current_state_as_feedback();
  return port_id;
}

/* ── state forwarding ────────────────────────────────────────────────────── */

bool midi::Processor::process_queues() {
  auto controllers_lock = std::lock_guard(_controllers_mutex);
  auto message_processed = false;
  for (const auto &binding : _receiver_bindings) {
    if (const auto message = binding.receiver->pop()) {
      binding.controller->process(*message);
      message_processed = true;
    }
  }
  return message_processed;
}

void midi::Processor::set_selected_channel(
    const controllers::ChannelType channel_type, const size_t channel_id) {
  std::lock_guard lock(_controllers_mutex);
  for (const auto &c : _controllers)
    c->set_selected_channel_from_processor(channel_type, channel_id);
}

void midi::Processor::clear_selected_channel() {
  std::lock_guard lock(_controllers_mutex);
  for (const auto &c : _controllers)
    c->clear_selected_channel_from_processor();
}

void midi::Processor::set_selected_plugin_page(const size_t plugin_id,
                                               const size_t page_number) {
  std::lock_guard lock(_controllers_mutex);
  for (const auto &c : _controllers)
    c->set_selected_plugin_page_from_processor(plugin_id, page_number);
}

void midi::Processor::set_selected_layer(const size_t layer_id) {
  std::lock_guard lock(_controllers_mutex);
  for (const auto &c : _controllers)
    c->set_layer_from_processor(layer_id);
}

void midi::Processor::set_channel_node(
    const controllers::ChannelType channel_type, const size_t channel_id,
    const uint64_t object_id) {
  std::lock_guard lock(_controllers_mutex);
  for (const auto &c : _controllers)
    c->set_channel_node(channel_type, channel_id, object_id);
}

void midi::Processor::set_master_channel_node(const size_t layer_id,
                                              const uint64_t object_id) {
  for (const auto &c : _controllers)
    c->set_master_channel_node(layer_id, object_id);
}

void midi::Processor::set_channel_filter_node(
    const controllers::ChannelType channel_type, const size_t channel_id,
    const uint64_t object_id) {
  std::lock_guard lock(_controllers_mutex);
  for (const auto &c : _controllers)
    c->set_channel_filter_node(channel_type, channel_id, object_id);
}

void midi::Processor::set_channel_send_node(
    const controllers::ChannelType channel_type, const size_t channel_id,
    const size_t send_id, const uint64_t object_id) {
  std::lock_guard lock(_controllers_mutex);
  for (const auto &c : _controllers)
    c->set_channel_send_node(channel_type, channel_id, send_id, object_id);
}

void midi::Processor::clear_filter_parameters(
    const controllers::ChannelType channel_type, const size_t channel_id) {
  std::lock_guard lock(_controllers_mutex);
  for (const auto &c : _controllers)
    c->clear_filter_parameters(channel_type, channel_id);
}

void midi::Processor::add_filter_parameter(
    const controllers::ChannelType channel_type, const size_t channel_id,
    const size_t plugin_id, char *name, const float min, const float max) {
  std::lock_guard lock(_controllers_mutex);
  for (const auto &c : _controllers)
    c->add_filter_parameter(channel_type, channel_id, plugin_id, name, min, max);
}

void midi::Processor::set_launchpad_mini_looper_slot_state(
    const controllers::ChannelType channel_type, const size_t channel_id,
    const size_t loop_position, const bool available, const bool loaded,
    const bool playing) {
  std::lock_guard lock(_controllers_mutex);
  for (const auto &c : _controllers)
    c->set_looper_slot_state(channel_type, channel_id, loop_position, available,
                             loaded, playing);
}
