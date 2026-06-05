#pragma once

#include "controllers/ChannelType.h"
#include "midi/Messages.h"

namespace controllers {

class IController {
public:
  virtual void process(const midi::Message &message) = 0;

  virtual void set_layer_from_processor(size_t layer) = 0;

  virtual void set_selected_channel_from_processor(ChannelType channel_type,
                                                   size_t channel_id) = 0;

  virtual void clear_selected_channel_from_processor() = 0;

  virtual void set_selected_plugin_page_from_processor(size_t plugin_id,
                                                       size_t page_number) = 0;

  virtual void add_current_state_as_feedback() = 0;

  virtual void set_master_channel_node(size_t layer_id, uint64_t object_id) = 0;

  virtual void set_channel_node(ChannelType channel_type, size_t channel_id,
                                uint64_t object_id) = 0;

  virtual void set_channel_filter_node(ChannelType channel_type,
                                       size_t channel_id,
                                       uint64_t object_id) = 0;

  virtual void set_channel_send_node(ChannelType channel_type,
                                     size_t channel_id, size_t send_id,
                                     uint64_t object_id) = 0;

  virtual void clear_filter_parameters(ChannelType channel_type,
                                       size_t channel_id) = 0;

  virtual void add_filter_parameter(ChannelType channel_type, size_t channel_id,
                                    size_t plugin_id, char *name, float min,
                                    float max) = 0;

  virtual void set_looper_slot_state(ChannelType channel_type,
                                     size_t channel_id, size_t loop_position,
                                     bool available, bool loaded,
                                     bool playing) {}

  virtual ~IController() = default;
};

} // namespace controllers
