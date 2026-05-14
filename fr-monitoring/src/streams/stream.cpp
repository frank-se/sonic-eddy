#include "streams/stream.h"

#include <format>
#include <iostream>
#include <ostream>
#include <string>

static void on_process(void *user_data) {
  auto stream = static_cast<streams::Stream *>(user_data);
  stream->process();
}

static void on_stream_param_changed(void *user_data, uint32_t id,
                                    const struct spa_pod *param) {
  auto stream = static_cast<streams::Stream *>(user_data);

  /* NULL means to clear the format */
  if (param == nullptr || id != SPA_PARAM_Format)
    return;

  if (spa_format_parse(param, &stream->format.media_type,
                       &stream->format.media_subtype) < 0)
    return;

  /* only accept raw audio */
  if (stream->format.media_type != SPA_MEDIA_TYPE_audio ||
      stream->format.media_subtype != SPA_MEDIA_SUBTYPE_raw)
    return;

  /* call a helper function to parse the format for us. */
  spa_format_audio_raw_parse(param, &stream->format.info.raw);
}

static void on_state_changed(void *user_data, enum pw_stream_state old,
                             enum pw_stream_state state, const char *error) {
  auto stream = static_cast<streams::Stream *>(user_data);

  if (state != PW_STREAM_STATE_STREAMING) {
    stream->reset();
  }
}

static const struct pw_stream_events stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .param_changed = on_stream_param_changed,
    .process = on_process,
};

void streams::Stream::process() {
  const auto pipewire_buffer = pw_stream_dequeue_buffer(_stream);

  if (pipewire_buffer == nullptr) {
    std::cerr << "ERROR: pipewire_buffer is nullptr!" << std::endl;
    return;
  }

  const auto buffer = pipewire_buffer->buffer;

  const auto samples = static_cast<const float *>(buffer->datas[0].data);

  if (samples == nullptr) {
    std::cerr << "ERROR: samples is nullptr!" << std::endl;
    return;
  }

  const auto number_of_channels = format.info.raw.channels;

  constexpr uint32_t max_channels = 2;

  const auto number_of_samples = buffer->datas[0].chunk->size / sizeof(float);

  std::array<float, max_channels> max{};
  std::array<float, max_channels> sum{};

  for (auto c = 0; c < std::min(number_of_channels, max_channels); c++) {
    for (auto i = c; i < number_of_samples; i += number_of_channels) {
      auto absolute_sample = std::abs(samples[i]);
      max[c] = std::max(max[c], absolute_sample);
      sum[c] = sum[c] + absolute_sample;
    }
  }

  _left_peak = max[0];
  _right_peak = max[1];

  const auto samples_per_channel = number_of_samples / number_of_channels;
  auto left_average = sum[0] / samples_per_channel;
  auto right_average = sum[1] / samples_per_channel;
  _left_average = left_average;
  _right_average = right_average;

  pw_stream_queue_buffer(_stream, pipewire_buffer);
}

void streams::Stream::setup() {
  const auto name = std::format("monitor {}", _object_serial);
  const auto target_object = std::to_string(_object_serial);

  const auto properties =
      pw_properties_new(PW_KEY_MEDIA_TYPE, "Audio", PW_KEY_MEDIA_CATEGORY,
                        "Capture", PW_KEY_MEDIA_ROLE, "DSP",
                        PW_KEY_TARGET_OBJECT, target_object.c_str(), NULL);

  _stream = pw_stream_new_simple(pw_main_loop_get_loop(_loop), name.c_str(),
                                 properties, &stream_events, this);

  const struct spa_pod *params[1];

  auto audio_info = SPA_AUDIO_INFO_RAW_INIT(.format = SPA_AUDIO_FORMAT_F32);

  uint8_t buffer[1024];
  auto b = SPA_POD_BUILDER_INIT(buffer, sizeof(buffer));

  params[0] = spa_format_audio_raw_build(&b, SPA_PARAM_EnumFormat, &audio_info);

  pw_stream_connect(_stream, PW_DIRECTION_INPUT, PW_ID_ANY,
                    static_cast<pw_stream_flags>(PW_STREAM_FLAG_AUTOCONNECT |
                                                 PW_STREAM_FLAG_MAP_BUFFERS |
                                                 PW_STREAM_FLAG_RT_PROCESS),
                    params, 1);
}
