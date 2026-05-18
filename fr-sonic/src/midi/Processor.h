#pragma once

#include "Registry.h"
#include "Receiver.h"
#include "Sender.h"
#include "controllers/ChannelType.h"
#include "controllers/MidiMix.h"

#include <condition_variable>
#include <mutex>
#include <thread>

namespace midi {

using LayerSelectCallback = std::function<void(size_t layer_id)>;
using ChannelSelectCallback =
    std::function<void(controllers::ChannelType, size_t channel_id)>;
using DialSectionModeCallback = std::function<void(
    controllers::ChannelType, size_t channel_id, controllers::DialMode)>;
using FilterParamsSectionSelectCallback = std::function<void(
    controllers::ChannelType, size_t channel_id, size_t section_id)>;
using FilterParamsSectionMovePagesRightCallback = std::function<void(uint64_t)>;
using FilterParamsSectionMovePagesLeftCallback  = std::function<void(uint64_t)>;
using ControllerPtr = std::shared_ptr<::controllers::IController>;

class Processor {
public:
  Processor(pw_loop *loop, pw_core *core,
            controllers::MidiControlChangeUpdateCallback callback)
      : _loop(loop),
        _registry(std::make_unique<Registry>(loop, core, callback)),
        _controller_update_callback(std::move(callback)) {}

  std::optional<size_t> create_midi_mix_port(
      const char *pmx_purpose, const char *pmx_tag,
      const LayerSelectCallback &layer_select_callback,
      const ChannelSelectCallback &channel_select_callback,
      const DialSectionModeCallback &dial_section_mode_select_callback,
      const FilterParamsSectionSelectCallback &filter_params_section_select_callback);

  std::optional<size_t> create_mm_1_port(
      const char *pmx_purpose, const char *pmx_tag,
      const LayerSelectCallback &layer_select_callback,
      const ChannelSelectCallback &channel_select_callback,
      const DialSectionModeCallback &dial_section_mode_select_callback,
      const FilterParamsSectionSelectCallback &filter_params_section_select_callback,
      const FilterParamsSectionMovePagesRightCallback &pages_right_callback,
      const FilterParamsSectionMovePagesLeftCallback  &pages_left_callback);

  std::optional<size_t> create_fader_fox_pc4_port(const char *pmx_purpose,
                                                  const char *pmx_tag);

  void start();
  void stop();
  bool process_queues();

  void set_selected_channel(controllers::ChannelType channel_type,
                            size_t channel_id);
  void clear_selected_channel();
  void set_selected_plugin_page(size_t plugin_id, size_t page_number);
  void set_selected_layer(size_t layer_id);
  void set_master_channel_node(size_t layer_id, uint64_t object_id);
  void set_channel_node(controllers::ChannelType channel_type,
                        size_t channel_id, uint64_t object_id);
  void set_channel_filter_node(controllers::ChannelType channel_type,
                               size_t channel_id, uint64_t object_id);
  void set_channel_send_node(controllers::ChannelType channel_type,
                             size_t channel_id, size_t send_id,
                             uint64_t object_id);
  void clear_filter_parameters(controllers::ChannelType channel_type,
                               size_t channel_id);
  void add_filter_parameter(controllers::ChannelType channel_type,
                            size_t channel_id, size_t plugin_id, char *name,
                            float min, float max);

private:
  pw_loop *_loop;
  std::unique_ptr<Registry> _registry;
  controllers::MidiControlChangeUpdateCallback _controller_update_callback;

  std::mutex _controllers_mutex;
  std::vector<ControllerPtr> _controllers{};
  Receivers _receivers{};

  std::thread _midi_processing_thread;
  std::mutex _queue_wait_mutex;
  std::condition_variable _queue_wait_condition;
  std::atomic<bool> _is_running{true};

  void start_processing_thread();
};

} // namespace midi
