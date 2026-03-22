#include "midi/Processor.h"

#include "controllers/CmdMm1.h"
#include "feedback/MidiMixFeedbackManager.h"

#include <iostream>
#include <utility>

void midi::Processor::start() {
  start_pipewire_thread();
  start_processing_thread();
}

void midi::Processor::stop() {
  pthread_kill(_pipewire_thread.native_handle(), SIGINT);
  _pipewire_thread.join();

  pthread_kill(_midi_processing_thread.native_handle(), SIGINT);
  _midi_processing_thread.join();
}

void midi::Processor::start_pipewire_thread() {
  std::binary_semaphore semaphore{0};
  _pipewire_thread = std::thread([this, &semaphore]() {
    setup_pipewire();
    semaphore.release();
    pw_main_loop_run(_loop);
  });
  semaphore.acquire();
}

static void do_quit(void *user_data, int signal_number) {
  const auto processor = static_cast<midi::Processor *>(user_data);
  processor->quit_main_loop();
}

void midi::Processor::quit_main_loop() const { pw_main_loop_quit(_loop); }

void midi::Processor::setup_pipewire() {
  pw_init(nullptr, nullptr);
  _loop = pw_main_loop_new(nullptr);

  pw_loop_add_signal(pw_main_loop_get_loop(_loop), SIGINT, do_quit, this);
  pw_loop_add_signal(pw_main_loop_get_loop(_loop), SIGTERM, do_quit, this);

  _context = pw_context_new(pw_main_loop_get_loop(_loop), nullptr, 0);
  _core = pw_context_connect(_context, nullptr, 0);
  _registry = pw_core_get_registry(_core, PW_VERSION_REGISTRY, 0);
}

static int setup_receiver_function(struct spa_loop *loop, bool async,
                                   uint32_t seq, const void *data, size_t size,
                                   void *user_data) {
  const auto receiver = static_cast<midi::Receiver *>(user_data);
  receiver->setup();
  return 0;
}

static int setup_sender_function(struct spa_loop *loop, bool async,
                                 uint32_t seq, const void *data, size_t size,
                                 void *user_data) {
  const auto sender = static_cast<midi::Sender *>(user_data);
  sender->setup();
  return 0;
}

size_t midi::Processor::create_midi_mix_port(
    const char *pmx_purpose, const char *pmx_tag,
    const std::function<void(size_t port_id, size_t layer_id)>
        &layer_select_callback,
    const std::function<void(size_t port_id, size_t channel_id)>
        &channel_select_callback,
    const std::function<void(size_t port_id, size_t channel_id,
                             controllers::DialMode)>
        &dial_section_mode_select_callback,
    const std::function<void(size_t port_id, size_t channel_id,
                             size_t section_id)>
        &filter_params_section_select_callback) {
  const auto receiver =
      std::make_shared<Receiver>(pmx_purpose, pmx_tag, _loop, MidiVersion::UMP,
                                 &_queue_wait_mutex, &_queue_wait_condition);

  pw_loop_invoke(pw_main_loop_get_loop(_loop), setup_receiver_function,
                 SPA_ID_INVALID, nullptr, 0, false, receiver.get());

  const auto sender = std::make_shared<Sender>(pmx_purpose, pmx_tag, _loop);

  pw_loop_invoke(pw_main_loop_get_loop(_loop), setup_sender_function,
                 SPA_ID_INVALID, nullptr, 0, false, sender.get());

  std::lock_guard action_containers_lock(_action_containers_mutex);
  const auto port_id = _action_containers.size();
  const auto midi_mix = std::make_shared<controllers::MidiMix>(
      _registry, _loop, sender,
      [port_id, layer_select_callback](const size_t layer_id) {
        layer_select_callback(port_id, layer_id);
      },
      [port_id, channel_select_callback](const size_t channel_id) {
        channel_select_callback(port_id, channel_id);
      },
      [port_id, dial_section_mode_select_callback](
          const size_t channel_id, const controllers::DialMode mode) {
        dial_section_mode_select_callback(port_id, channel_id, mode);
      },
      [port_id, filter_params_section_select_callback](
          const size_t channel_id, const size_t section_id) {
        filter_params_section_select_callback(port_id, channel_id, section_id);
      });

  _action_containers.push_back(midi_mix);

  std::lock_guard _receivers_lock(_receivers_mutex);
  _receivers.push_back(receiver);

  std::lock_guard _senders_lock(_senders_mutex);
  _senders.push_back(sender);

  return port_id;
}

size_t midi::Processor::create_mm_1_port(
    const char *pmx_purpose, const char *pmx_tag,
    const LayerSelectCallback &layer_select_callback,
    const ChannelSelectCallback &channel_select_callback,
    const DialSectionModeCallback &dial_section_mode_select_callback,
    const FilterParamsSectionSelectCallback
        &filter_params_section_select_callback,
    const FilterParamsSectionMovePagesRightCallback
        &filter_params_section_move_pages_right_callback,
    const FilterParamsSectionMovePagesLeftCallback
        &filter_params_section_move_pages_left_callback) {

  const auto receiver =
      std::make_shared<Receiver>(pmx_purpose, pmx_tag, _loop, MidiVersion::Midi,
                                 &_queue_wait_mutex, &_queue_wait_condition);

  pw_loop_invoke(pw_main_loop_get_loop(_loop), setup_receiver_function,
                 SPA_ID_INVALID, nullptr, 0, false, receiver.get());

  const auto sender = std::make_shared<Sender>(pmx_purpose, pmx_tag, _loop);

  pw_loop_invoke(pw_main_loop_get_loop(_loop), setup_sender_function,
                 SPA_ID_INVALID, nullptr, 0, false, sender.get());

  std::lock_guard action_containers_lock(_action_containers_mutex);
  const auto port_id = _action_containers.size();

  const auto cmd_mm_1 = std::make_shared<controllers::CmdMm1>(
      _registry, _loop, sender,
      [port_id, layer_select_callback](const size_t layer_id) {
        layer_select_callback(port_id, layer_id);
      },
      [port_id, channel_select_callback](const size_t channel_id) {
        channel_select_callback(port_id, channel_id);
      },
      [port_id, dial_section_mode_select_callback](
          const size_t channel_id, const controllers::DialMode dial_mode) {
        dial_section_mode_select_callback(port_id, channel_id, dial_mode);
      },
      [port_id, filter_params_section_select_callback](
          size_t channel_id, const size_t section_id) {
        filter_params_section_select_callback(port_id, channel_id, section_id);
      },
      [port_id, filter_params_section_move_pages_right_callback](
          const uint64_t step_count) {
        filter_params_section_move_pages_right_callback(port_id, step_count);
      },
      [port_id, filter_params_section_move_pages_left_callback](
          const uint64_t step_count) {
        filter_params_section_move_pages_left_callback(port_id, step_count);
      });

  _action_containers.push_back(cmd_mm_1);

  std::lock_guard _receivers_lock(_receivers_mutex);
  _receivers.push_back(receiver);

  std::lock_guard _senders_lock(_senders_mutex);
  _senders.push_back(sender);

  return port_id;
}

void midi::Processor::start_processing_thread() {
  _midi_processing_thread = std::thread([this]() {
    while (true) {
      while (process_queues()) {
      }

      auto lock = std::unique_lock(_queue_wait_mutex);
      _queue_wait_condition.wait(lock);
    }
  });
}

bool midi::Processor::process_queues() {
  auto receivers_lock = std::lock_guard(_receivers_mutex);
  auto action_containers_lock = std::lock_guard(_action_containers_mutex);

  auto message_processed = false;
  for (size_t i = 0; i < _receivers.size(); i++) {
    const auto receiver = _receivers[i];
    const auto message = receiver->pop();

    if (message) {
      const auto action_container = _action_containers[i];
      action_container->process(*message);
      message_processed = true;
    }
  }

  return message_processed;
}
