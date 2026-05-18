#include "Sender.h"

#include "logging/log.h"
#include "build_midi.h"
#include "build_ump.h"

#include <cstring>
#include <iostream>
#include <spa/control/control.h>
#include <spa/param/format.h>
#include <spa/param/tag-utils.h>

static void on_process_sender(void *user_data) {
  const auto sender = static_cast<midi::Sender *>(user_data);
  sender->process();
}

static constexpr pw_stream_events stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .process = on_process_sender,
};

void midi::Sender::process() {
  if (_queue.empty())
    return;

  const auto pipewire_buffer = pw_stream_dequeue_buffer(_stream);

  if (pipewire_buffer == nullptr) {
    logging::log<logging::LogLevel::Error>("pipewire_buffer is nullptr");
    return;
  }

  const auto buffer = pipewire_buffer->buffer;

  if (buffer->n_datas != 1) {
    logging::log<logging::LogLevel::Error>("Unexpected number of buffers {}",
                                           buffer->n_datas);
    pw_stream_queue_buffer(_stream, pipewire_buffer);
    return;
  }

  if (buffer->datas[0].data == nullptr) {
    logging::log<logging::LogLevel::Error>("Data is nullptr");
    pw_stream_queue_buffer(_stream, pipewire_buffer);
    return;
  }

  auto *spa_data = &buffer->datas[0];
  spa_data->chunk->offset = 0;
  spa_data->chunk->size = 0;
  spa_data->chunk->stride = 1;
  spa_data->chunk->flags = 0;

  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, spa_data->data, spa_data->maxsize);

  spa_pod_frame sequence_frame{};
  spa_pod_builder_push_sequence(&builder, &sequence_frame, 0);

  _queue.consume_all([&builder](auto &&item) {
    auto message = build_ump(item);
    std::visit(
        [&builder](auto &&arg) {
          using T = std::decay_t<decltype(arg)>;
          if constexpr (std::is_same_v<T, uint32_t>) {
            spa_pod_builder_control(&builder, 0, SPA_CONTROL_UMP);
            spa_pod_builder_bytes(&builder, &arg, 4);
          } else if constexpr (std::is_same_v<T, std::array<uint32_t, 2>>) {
            spa_pod_builder_control(&builder, 0, SPA_CONTROL_UMP);
            spa_pod_builder_bytes(&builder, arg.data(), 8);
          }
        },
        message);
  });

  spa_pod_builder_pop(&builder, &sequence_frame);
  spa_data->chunk->size = builder.state.offset;

  pw_stream_queue_buffer(_stream, pipewire_buffer);
}

void midi::Sender::setup() {
  logging::log<logging::LogLevel::Trace>("Sender::setup");

  const auto properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Midi", PW_KEY_MEDIA_CATEGORY, "Playback",
      PW_KEY_MEDIA_ROLE, "DSP", PW_KEY_MEDIA_CLASS, "Stream/Output/Midi",
      PW_KEY_FORMAT_DSP, "32 bit raw UMP", "pmx.purpose", _pmx_purpose.c_str(),
      "pmx.tag", _pmx_tag.c_str(), nullptr);

  _stream = pw_stream_new_simple(_loop, _name.c_str(),
                                 properties, &stream_events, this);

  const spa_pod *param[1];

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

  pw_stream_connect(_stream, PW_DIRECTION_OUTPUT, PW_ID_ANY,
                    static_cast<pw_stream_flags>(PW_STREAM_FLAG_AUTOCONNECT |
                                                 PW_STREAM_FLAG_MAP_BUFFERS |
                                                 PW_STREAM_FLAG_RT_PROCESS),
                    param, 1);
}

void midi::Sender::push(const Message message) { _queue.push(message); }
