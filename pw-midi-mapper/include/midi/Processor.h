#pragma once

#include "Sender.h"
#include "controllers/MidiMix.h"
#include "layers/LayerManager.h"
#include "midi/ActionContainer.h"
#include "midi/Receiver.h"

#include <condition_variable>
#include <mutex>

namespace midi {

using LayerSelectCallback =
    std::function<void(size_t port_id, size_t layer_id)>;

using ChannelSelectCallback =
    std::function<void(size_t port_id, size_t channel_id)>;

using DialSectionModeCallback = std::function<void(
    size_t port_id, size_t channel_id, controllers::DialMode)>;

using FilterParamsSectionSelectCallback =
    std::function<void(size_t port_id, size_t channel_id, size_t section_id)>;

using FilterParamsSectionMovePagesRightCallback =
    std::function<void(size_t port_id, uint64_t)>;

using FilterParamsSectionMovePagesLeftCallback =
    std::function<void(size_t port_id, uint64_t)>;

using ActionContainerPtr =
    std::shared_ptr<::action_container::IActionContainer>;

class Processor {
public:
  explicit Processor() = default;

  size_t create_midi_mix_port(
      const char *pmx_purpose, const char *pmx_tag,
      const LayerSelectCallback &layer_select_callback,
      const ChannelSelectCallback &channel_select_callback,
      const DialSectionModeCallback &dial_section_mode_select_callback,
      const FilterParamsSectionSelectCallback
          &filter_params_section_select_callback);

  size_t create_mm_1_port(
      const char *pmx_purpose, const char *pmx_tag,
      const LayerSelectCallback &layer_select_callback,
      const ChannelSelectCallback &channel_select_callback,
      const DialSectionModeCallback &dial_section_mode_select_callback,
      const FilterParamsSectionSelectCallback
          &filter_params_section_select_callback,
      const FilterParamsSectionMovePagesRightCallback
          &filter_params_section_move_pages_right_callback,
      const FilterParamsSectionMovePagesLeftCallback
          &filter_params_section_move_pages_left_callback);

  void start();

  void stop();

  bool process_queues();

  void quit_main_loop() const;

private:
  std::mutex _action_containers_mutex;
  std::vector<ActionContainerPtr> _action_containers{};

  std::mutex _layer_managers_mutex;
  std::vector<layers::LayerManagerPtr> _layer_managers{};

  std::mutex _receivers_mutex;
  Receivers _receivers{};

  std::mutex _senders_mutex;
  Senders _senders{};

  pw_main_loop *_loop = nullptr;
  pw_context *_context = nullptr;
  pw_core *_core = nullptr;
  pw_registry *_registry = nullptr;

  std::thread _midi_processing_thread;
  std::thread _pipewire_thread;
  std::mutex _queue_wait_mutex;
  std::condition_variable _queue_wait_condition;

  void start_processing_thread();
  void start_pipewire_thread();
  void setup_pipewire();
};

} // namespace midi
