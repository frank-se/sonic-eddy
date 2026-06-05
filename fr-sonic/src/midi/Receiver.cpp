#include "Receiver.h"

#include "logging/log.h"
#include "parse_midi.h"
#include "parse_ump.h"

#include <spa/control/control.h>
#include <spa/param/format.h>
#include <spa/param/tag-utils.h>

#include <format>
#include <iostream>

static void on_process_receiver(void *user_data) {
  const auto receiver = static_cast<midi::Receiver *>(user_data);
  receiver->process();
}

static const struct pw_stream_events stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .process = on_process_receiver,
};

void midi::Receiver::process() {
  const auto pipewire_buffer = pw_stream_dequeue_buffer(_stream);

  if (pipewire_buffer == nullptr) {
    std::cerr << "ERROR: pipewire_buffer is nullptr!" << std::endl;
    return;
  }

  const auto buffer = pipewire_buffer->buffer;

  if (buffer->n_datas != 1) {
    std::cerr << "Unexpected number of buffers! - " << buffer->n_datas
              << std::endl;
    pw_stream_queue_buffer(_stream, pipewire_buffer);
    return;
  }

  if (buffer->datas[0].data == nullptr) {
    std::cerr << "ERROR: data is nullptr!" << std::endl;
    pw_stream_queue_buffer(_stream, pipewire_buffer);
    return;
  }

  auto pod = static_cast<struct spa_pod *>(spa_pod_from_data(
      buffer->datas[0].data, buffer->datas[0].maxsize,
      buffer->datas[0].chunk->offset, buffer->datas[0].chunk->size));

  if (!spa_pod_is_sequence(pod)) {
    std::cerr << "Pod is not a sequence" << std::endl;
    pw_stream_queue_buffer(_stream, pipewire_buffer);
    return;
  }

  auto sequence = reinterpret_cast<struct spa_pod_sequence *>(pod);
  spa_pod_control *pod_control;
  SPA_POD_SEQUENCE_FOREACH(sequence, pod_control) {
    if (pod_control->type == SPA_CONTROL_Midi) {
      const void *data = SPA_POD_BODY(&pod_control->value);
      uint32_t length = SPA_POD_BODY_SIZE(&pod_control->value);

      if (length != 3 && length != 2) {
        continue;
      }

      if (auto midi_message =
              parse_midi(static_cast<const uint8_t *>(data), length)) {
        _queue.push(*midi_message);
        _queue_wait_condition->notify_all();
      }
    } else if (pod_control->type == SPA_CONTROL_UMP) {
      const void *data = SPA_POD_BODY(&pod_control->value);
      const uint32_t length = SPA_POD_BODY_SIZE(&pod_control->value);

      if (length != 4 && length != 8 && length != 12 && length != 16) {
        continue;
      }

      if (auto midi_message = parse_ump(data, length)) {
        _queue.push(*midi_message);
        _queue_wait_condition->notify_all();
      }
    }
  }

  pw_stream_queue_buffer(_stream, pipewire_buffer);
}

void midi::Receiver::setup() {
  logging::log<logging::LogLevel::Trace>("Receiver::setup");

  const std::string midi_format =
      _midi_version == MidiVersion::UMP ? "32 bit raw UMP" : "8 bit raw midi";

  const auto properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Midi", PW_KEY_MEDIA_CATEGORY, "Capture",
      PW_KEY_MEDIA_ROLE, "DSP", PW_KEY_MEDIA_CLASS, "Stream/Input/Midi",
      PW_KEY_FORMAT_DSP, midi_format.c_str(), "pmx.purpose",
      _pmx_purpose.c_str(), "pmx.tag", _pmx_tag.c_str(), nullptr);

  _stream = pw_stream_new_simple(_loop, _name.c_str(),
                                 properties, &stream_events, this);

  const struct spa_pod *param[1];

  uint8_t buffer[256];
  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, buffer, sizeof(buffer));

  spa_pod_frame frame{};
  spa_pod_builder_push_object(&builder, &frame, SPA_TYPE_OBJECT_Format,
                              SPA_PARAM_EnumFormat);

  spa_pod_builder_add(
      &builder, SPA_FORMAT_mediaType, SPA_POD_Id(SPA_MEDIA_TYPE_application),
      SPA_FORMAT_mediaSubtype, SPA_POD_Id(SPA_MEDIA_SUBTYPE_control), 0);

  param[0] =
      static_cast<const spa_pod *>(spa_pod_builder_pop(&builder, &frame));

  pw_stream_connect(_stream, PW_DIRECTION_INPUT, PW_ID_ANY,
                    static_cast<pw_stream_flags>(PW_STREAM_FLAG_AUTOCONNECT |
                                                 PW_STREAM_FLAG_MAP_BUFFERS |
                                                 PW_STREAM_FLAG_RT_PROCESS),
                    param, 1);
}

std::optional<midi::Message> midi::Receiver::pop() {
  return _queue.pop(boost::lockfree::uses_optional);
}
