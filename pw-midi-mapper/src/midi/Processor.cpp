#include "midi/Processor.h"
#include "controllers/CmdMm1.h"
#include "controllers/FaderfoxPc4.h"
#include "logging/log.h"

void midi::Processor::start() {
  logging::log<logging::LogLevel::Trace>("Processor::start");

  start_pipewire_thread();
  start_processing_thread();
}

void midi::Processor::stop() {
  logging::log<logging::LogLevel::Trace>("Processor::stop");

  _is_running = false;

  pthread_kill(_pipewire_thread.native_handle(), SIGINT);
  _pipewire_thread.join();

  _midi_processing_thread.join();
}

void midi::Processor::start_pipewire_thread() {
  logging::log<logging::LogLevel::Trace>("Processor::start_pipewire_thread");

  std::binary_semaphore semaphore{0};
  _pipewire_thread = std::thread([this, &semaphore]() {
    _pipewire =
        std::make_unique<pipewire::Pipewire>(_controller_update_callback);
    semaphore.release();
    _pipewire->run();
  });
  semaphore.acquire();
}

static int setup_receiver_function(spa_loop *loop, bool async, uint32_t seq,
                                   const void *data, size_t size,
                                   void *user_data) {
  logging::log<logging::LogLevel::Trace>("Processor::setup_receiver_function");

  const auto receiver = static_cast<midi::Receiver *>(user_data);
  receiver->setup();
  return 0;
}

static int setup_sender_function(spa_loop *loop, bool async, uint32_t seq,
                                 const void *data, size_t size,
                                 void *user_data) {
  logging::log<logging::LogLevel::Trace>("Processor::setup_sender_function");

  const auto sender = static_cast<midi::Sender *>(user_data);
  sender->setup();
  return 0;
}

std::optional<size_t> midi::Processor::create_midi_mix_port(
    const char *pmx_purpose, const char *pmx_tag,
    const std::function<void(size_t layer_id)> &layer_select_callback,
    const std::function<void(controllers::ChannelType, size_t channel_id)>
        &channel_select_callback,
    const std::function<void(controllers::ChannelType, size_t channel_id,
                             controllers::DialMode)>
        &dial_section_mode_select_callback,
    const std::function<void(controllers::ChannelType, size_t channel_id,
                             size_t section_id)>
        &filter_params_section_select_callback) {
  logging::log<logging::LogLevel::Trace>("Processor::create_midi_mix_port");

  if (_pipewire == nullptr) {
    logging::log<logging::LogLevel::Error>(
        "Couldn't create midi mix port, pipewire not set up!");

    return std::nullopt;
  }

  std::lock_guard controllers_lock(_controllers_mutex);
  const auto port_id = _controllers.size();

  const auto receiver_name = std::format("midi-receiver {} Midi Mix", port_id);
  const auto sender_name = std::format("midi-sender {} Midi Mix", port_id);

  const auto receiver = std::make_shared<Receiver>(
      pmx_purpose, pmx_tag, receiver_name, _pipewire->loop(), MidiVersion::UMP,
      &_queue_wait_mutex, &_queue_wait_condition);

  pw_loop_invoke(pw_main_loop_get_loop(_pipewire->loop()),
                 setup_receiver_function, SPA_ID_INVALID, nullptr, 0, false,
                 receiver.get());

  const auto sender = std::make_shared<Sender>(pmx_purpose, pmx_tag,
                                               sender_name, _pipewire->loop());

  pw_loop_invoke(pw_main_loop_get_loop(_pipewire->loop()),
                 setup_sender_function, SPA_ID_INVALID, nullptr, 0, false,
                 sender.get());

  const auto midi_mix = std::make_shared<controllers::MidiMix>(
      _pipewire->registry(), _pipewire->loop(), sender,
      [this, layer_select_callback](const size_t layer_id) {
        for (const auto &action_container : _controllers) {
          action_container->set_layer_from_processor(layer_id);
        }

        layer_select_callback(layer_id);
      },
      [this, channel_select_callback](const size_t channel_id) {
        for (const auto &action_container : _controllers) {
          action_container->set_selected_channel_from_processor(
              controllers::ChannelType::CHANNEL, channel_id);
        }
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

  _receivers.push_back(receiver);

  midi_mix->add_current_state_as_feedback();

  return port_id;
}

std::optional<size_t>
midi::Processor::create_fader_fox_pc4_port(const char *pmx_purpose,
                                           const char *pmx_tag) {
  logging::log<logging::LogLevel::Trace>(
      "Processor::create_fader_fox_pc4_port");

  if (_pipewire == nullptr) {
    logging::log<logging::LogLevel::Error>(
        "Couldn't create Fader Fox PC4 port, pipewire not set up!");

    return std::nullopt;
  }

  std::lock_guard controllers_lock(_controllers_mutex);
  const auto port_id = _controllers.size();

  const auto receiver_name = std::format("midi-receiver {} FF PC4", port_id);

  const auto receiver = std::make_shared<Receiver>(
      pmx_purpose, pmx_tag, receiver_name, _pipewire->loop(), MidiVersion::UMP,
      &_queue_wait_mutex, &_queue_wait_condition);

  pw_loop_invoke(pw_main_loop_get_loop(_pipewire->loop()),
                 setup_receiver_function, SPA_ID_INVALID, nullptr, 0, false,
                 receiver.get());

  const auto faderfox_pc4 = std::make_shared<controllers::FaderfoxPc4>(
      _pipewire->registry(), _pipewire->loop());

  _controllers.push_back(faderfox_pc4);

  _receivers.push_back(receiver);

  return port_id;
}

std::optional<size_t> midi::Processor::create_mm_1_port(
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
  logging::log<logging::LogLevel::Trace>("Processor::create_mm_1_port");

  if (_pipewire == nullptr) {
    logging::log<logging::LogLevel::Error>(
        "Couldn't create CMD MM-1 port, pipewire not set up!");

    return std::nullopt;
  }

  std::lock_guard controllers_lock(_controllers_mutex);
  const auto port_id = _controllers.size();

  const auto receiver_name = std::format("midi-receiver {} MM1", port_id);
  const auto sender_name = std::format("midi-sender {} MM1", port_id);

  const auto receiver = std::make_shared<Receiver>(
      pmx_purpose, pmx_tag, receiver_name, _pipewire->loop(), MidiVersion::UMP,
      &_queue_wait_mutex, &_queue_wait_condition);

  pw_loop_invoke(pw_main_loop_get_loop(_pipewire->loop()),
                 setup_receiver_function, SPA_ID_INVALID, nullptr, 0, false,
                 receiver.get());

  const auto sender = std::make_shared<Sender>(pmx_purpose, pmx_tag,
                                               sender_name, _pipewire->loop());

  pw_loop_invoke(pw_main_loop_get_loop(_pipewire->loop()),
                 setup_sender_function, SPA_ID_INVALID, nullptr, 0, false,
                 sender.get());

  const auto cmd_mm_1 = std::make_shared<controllers::CmdMm1>(
      _pipewire->registry(), _pipewire->loop(), sender,
      [this, layer_select_callback](const size_t layer_id) {
        for (const auto &action_container : _controllers) {
          action_container->set_layer_from_processor(layer_id);
        }

        layer_select_callback(layer_id);
      },
      [this, channel_select_callback](const size_t channel_id) {
        for (const auto &action_container : _controllers) {
          action_container->set_selected_channel_from_processor(
              controllers::ChannelType::GROUP_CHANNEL, channel_id);
        }

        channel_select_callback(controllers::ChannelType::GROUP_CHANNEL,
                                channel_id);
      },
      [dial_section_mode_select_callback](
          const size_t channel_id, const controllers::DialMode dial_mode) {
        dial_section_mode_select_callback(
            controllers::ChannelType::GROUP_CHANNEL, channel_id, dial_mode);
      },
      [filter_params_section_select_callback](const size_t channel_id,
                                              const size_t section_id) {
        filter_params_section_select_callback(
            controllers::ChannelType::GROUP_CHANNEL, channel_id, section_id);
      },
      [filter_params_section_move_pages_right_callback](
          const uint64_t step_count) {
        filter_params_section_move_pages_right_callback(step_count);
      },
      [filter_params_section_move_pages_left_callback](
          const uint64_t step_count) {
        filter_params_section_move_pages_left_callback(step_count);
  },
  _controller_update_callback);

  _controllers.push_back(cmd_mm_1);

  _receivers.push_back(receiver);

  cmd_mm_1->add_current_state_as_feedback();
  return port_id;
}

void midi::Processor::start_processing_thread() {
  logging::log<logging::LogLevel::Trace>("Processor::start_processing_thread");

  _midi_processing_thread = std::thread([this]() {
    while (_is_running) {
      while (process_queues()) {
        // Keep processing until no receiver queue has messages anymore
      }

      auto lock = std::unique_lock(_queue_wait_mutex);
      _queue_wait_condition.wait_for(lock, std::chrono::seconds(1));
    }
  });
}

bool midi::Processor::process_queues() {
  auto controllers_lock = std::lock_guard(_controllers_mutex);

  auto message_processed = false;
  for (size_t i = 0; i < _receivers.size(); i++) {
    const auto receiver = _receivers[i];

    if (const auto message = receiver->pop()) {
      const auto action_container = _controllers[i];
      action_container->process(*message);
      message_processed = true;
    }
  }

  return message_processed;
}

void midi::Processor::set_selected_channel(
    const controllers::ChannelType channel_type, const size_t channel_id) {
  logging::log<logging::LogLevel::Trace>("Processor::set_selected_channel");

  std::lock_guard controllers_lock(_controllers_mutex);

  for (const auto &action_container : _controllers) {
    action_container->set_selected_channel_from_processor(channel_type,
                                                          channel_id);
  }
}

void midi::Processor::clear_selected_channel() {
  logging::log<logging::LogLevel::Trace>("Processor::clear_selected_channel");

  std::lock_guard controllers_lock(_controllers_mutex);

  for (const auto &action_container : _controllers) {
    action_container->clear_selected_channel_from_processor();
  }
}

void midi::Processor::set_selected_plugin_page(const size_t plugin_id,
                                               const size_t page_number) {
  logging::log<logging::LogLevel::Trace>("Processor::set_selected_plugin_page");

  std::lock_guard controllers_lock(_controllers_mutex);

  for (const auto &action_container : _controllers) {
    action_container->set_selected_plugin_page_from_processor(plugin_id,
                                                              page_number);
  }
}

void midi::Processor::set_selected_layer(const size_t layer_id) {
  logging::log<logging::LogLevel::Trace>("Processor::set_selected_layer");

  std::lock_guard controllers_lock(_controllers_mutex);

  size_t index = 0;
  for (const auto &controller : _controllers) {
    logging::log<logging::LogLevel::Trace>("Setting layer {} for controller {}",
                                           layer_id, index);

    controller->set_layer_from_processor(layer_id);
    index++;
  }
}

void midi::Processor::set_channel_node(
    const controllers::ChannelType channel_type, const size_t channel_id,
    const uint64_t object_id) {
  logging::log<logging::LogLevel::Trace>("Processor::set_channel_node");

  std::lock_guard controllers_lock(_controllers_mutex);

  for (const auto &controller : _controllers) {
    controller->set_channel_node(channel_type, channel_id, object_id);
  }
}

void midi::Processor::set_channel_filter_node(
    const controllers::ChannelType channel_type, const size_t channel_id,
    const uint64_t object_id) {
  logging::log<logging::LogLevel::Trace>("Processor::set_channel_filter_node");

  std::lock_guard controllers_lock(_controllers_mutex);

  for (const auto &controller : _controllers) {
    controller->set_channel_filter_node(channel_type, channel_id, object_id);
  }
}

void midi::Processor::set_channel_send_node(
    const controllers::ChannelType channel_type, const size_t channel_id,
    const size_t send_id, const uint64_t object_id) {
  logging::log<logging::LogLevel::Trace>("Processor::set_channel_send_node");

  std::lock_guard controllers_lock(_controllers_mutex);

  for (const auto &controller : _controllers) {
    controller->set_channel_send_node(channel_type, channel_id, send_id,
                                      object_id);
  }
}

void midi::Processor::clear_filter_parameters(
    const controllers::ChannelType channel_type, const size_t channel_id) {
  logging::log<logging::LogLevel::Trace>("Processor::clear_filter_parameters");

  std::lock_guard controllers_lock(_controllers_mutex);

  for (const auto &controller : _controllers) {
    controller->clear_filter_parameters(channel_type, channel_id);
  }
}

void midi::Processor::add_filter_parameter(
    const controllers::ChannelType channel_type, const size_t channel_id,
    const size_t plugin_id, char *name, const float min, const float max) {
  logging::log<logging::LogLevel::Trace>("Processor::add_filter_parameter");

  std::lock_guard controllers_lock(_controllers_mutex);

  for (const auto &controller : _controllers) {
    controller->add_filter_parameter(channel_type, channel_id, plugin_id, name,
                                     min, max);
  }
}

void midi::Processor::set_master_channel_node(const size_t layer_id,
                                              const uint64_t object_id) {
  logging::log<logging::LogLevel::Trace>("Processor::set_master_channel_node");

  for (const auto &controller : _controllers) {
    controller->set_master_channel_node(layer_id, object_id);
  }
}
