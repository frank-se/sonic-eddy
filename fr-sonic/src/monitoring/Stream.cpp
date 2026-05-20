#include "Stream.h"

#include <algorithm>
#include <spa/param/props.h>
#include <cmath>
#include <format>
#include <iostream>
#include <string>

static void on_process(void *user_data) {
  auto stream = static_cast<monitoring::Stream *>(user_data);
  stream->process();
}

static void on_state_changed(void *user_data,
                             enum pw_stream_state old_state,
                             enum pw_stream_state state,
                             const char *error) {
  (void)old_state; (void)error;
  if (state != PW_STREAM_STATE_PAUSED)
    return;
  auto *stream = static_cast<monitoring::Stream *>(user_data);
  float volumes[2] = {1.0f, 1.0f};
  pw_stream_set_control(stream->get_stream(), SPA_PROP_channelVolumes, 2, volumes);
}

static void on_stream_param_changed(void *user_data, uint32_t id,
                                    const struct spa_pod *param) {
  auto stream = static_cast<monitoring::Stream *>(user_data);

  if (param == nullptr || id != SPA_PARAM_Format)
    return;

  if (spa_format_parse(param, &stream->format.media_type,
                       &stream->format.media_subtype) < 0)
    return;

  if (stream->format.media_type != SPA_MEDIA_TYPE_audio ||
      stream->format.media_subtype != SPA_MEDIA_SUBTYPE_raw)
    return;

  spa_format_audio_raw_parse(param, &stream->format.info.raw);
}

static const struct pw_stream_events stream_events = {
    .version       = PW_VERSION_STREAM_EVENTS,
    .state_changed = on_state_changed,
    .param_changed = on_stream_param_changed,
    .process       = on_process,
};

void monitoring::Stream::process() {
  const auto pipewire_buffer = pw_stream_dequeue_buffer(_stream);

  if (pipewire_buffer == nullptr) {
    std::cerr << "ERROR: pipewire_buffer is nullptr!" << std::endl;
    return;
  }

  const auto buffer  = pipewire_buffer->buffer;
  const auto samples = static_cast<const float *>(buffer->datas[0].data);

  if (samples == nullptr) {
    std::cerr << "ERROR: samples is nullptr!" << std::endl;
    pw_stream_queue_buffer(_stream, pipewire_buffer);
    return;
  }

  const auto number_of_channels = format.info.raw.channels;
  constexpr uint32_t max_channels = 2;
  const auto number_of_samples = buffer->datas[0].chunk->size / sizeof(float);
  const auto ch = std::min(number_of_channels, max_channels);

  BufferEntry entry{};
  entry.timestamp = std::chrono::steady_clock::now();
  entry.samples   = static_cast<uint32_t>(number_of_samples / (ch > 0 ? ch : 1));

  for (uint32_t c = 0; c < ch; c++) {
    for (auto i = c; i < number_of_samples; i += number_of_channels) {
      const float s = samples[i];
      const float a = std::abs(s);
      if (a > entry.peak[c]) entry.peak[c] = a;
      entry.sum_sq[c] += s * s;
    }
  }

  _queue.push(entry);  // wait-free; silently drops if queue is full

  pw_stream_queue_buffer(_stream, pipewire_buffer);
}

void monitoring::Stream::compute_metrics(uint32_t window_ms) {
  using namespace std::chrono;
  const auto now    = steady_clock::now();
  const auto cutoff = now - milliseconds(window_ms);
  const auto hold   = milliseconds(HOLD_DURATION_MS);

  // Drain any new entries from the RT thread into the window vector.
  _queue.consume_all([this](const BufferEntry &e) {
    _window.push_back(e);
  });

  // Evict entries older than the window.
  std::erase_if(_window, [&cutoff](const BufferEntry &e) {
    return e.timestamp < cutoff;
  });

  float window_peak[2]   = {};
  double total_sum_sq[2] = {};
  uint64_t total_samples = 0;

  for (const auto &e : _window) {
    for (int c = 0; c < 2; c++) {
      if (e.peak[c] > window_peak[c]) window_peak[c] = e.peak[c];
      total_sum_sq[c] += e.sum_sq[c];
    }
    total_samples += e.samples;
  }

  _left_rms  = total_samples > 0
      ? static_cast<float>(std::sqrt(total_sum_sq[0] / total_samples)) : 0.0f;
  _right_rms = total_samples > 0
      ? static_cast<float>(std::sqrt(total_sum_sq[1] / total_samples)) : 0.0f;

  // Peak hold per channel.
  if (window_peak[0] >= _left_held_peak) {
    _left_held_peak      = window_peak[0];
    _left_held_peak_time = now;
  } else if (now - _left_held_peak_time > hold) {
    _left_held_peak = window_peak[0];
  }

  if (window_peak[1] >= _right_held_peak) {
    _right_held_peak      = window_peak[1];
    _right_held_peak_time = now;
  } else if (now - _right_held_peak_time > hold) {
    _right_held_peak = window_peak[1];
  }
}

void monitoring::Stream::setup() {
  const auto name          = std::format("monitor {}", _object_serial);
  const auto target_object = std::to_string(_object_serial);

  const auto properties =
      pw_properties_new(PW_KEY_MEDIA_TYPE, "Audio", PW_KEY_MEDIA_CATEGORY,
                        "Capture", PW_KEY_MEDIA_ROLE, "DSP",
                        PW_KEY_TARGET_OBJECT, target_object.c_str(), NULL);

  _stream = pw_stream_new_simple(_loop, name.c_str(), properties,
                                 &stream_events, this);

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
