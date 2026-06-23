#include "Stream.h"

#include <algorithm>
#include <chrono>
#include <cmath>
#include <cstring>
#include <spa/param/props.h>
#include <spa/param/audio/format-utils.h>
#include <pipewire/pipewire.h>

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

  if (pipewire_buffer == nullptr)
    return;

  const auto buffer  = pipewire_buffer->buffer;
  const auto samples = static_cast<const float *>(buffer->datas[0].data);

  if (samples != nullptr) {
    const auto raw_samples = buffer->datas[0].chunk->size / sizeof(float);
    const auto n_samples   = static_cast<uint32_t>(
        std::min(raw_samples, static_cast<size_t>(MAX_RAW_SAMPLES)));

    RawEntry entry{};
    entry.timestamp     = std::chrono::steady_clock::now();
    entry.n_samples     = n_samples;
    entry.channel_count = format.info.raw.channels;
    std::memcpy(entry.samples, samples, n_samples * sizeof(float));

    _queue.push(entry);
  }

  pw_stream_queue_buffer(_stream, pipewire_buffer);
}

void monitoring::Stream::compute_metrics(uint32_t window_ms) {
  using namespace std::chrono;
  const auto now    = steady_clock::now();
  const auto cutoff = now - milliseconds(window_ms);
  const auto hold   = milliseconds(HOLD_DURATION_MS);

  // Drain raw entries, compute peak/sum_sq, push compact entries into window.
  _queue.consume_all([this](const RawEntry &raw) {
    BufferEntry e{};
    e.timestamp = raw.timestamp;
    const auto ch     = std::min(raw.channel_count, 2u);
    const auto stride = raw.channel_count > 0 ? raw.channel_count : 1;
    e.frames          = raw.n_samples / stride;

    for (uint32_t c = 0; c < ch; c++) {
      for (uint32_t i = c; i < raw.n_samples; i += stride) {
        const float s = raw.samples[i];
        e.peak[c]    = std::max(e.peak[c], std::abs(s));
        e.sum_sq[c] += static_cast<double>(s) * static_cast<double>(s);
      }
    }
    _window.push_back(e);
  });

  // Evict entries older than the window.
  std::erase_if(_window, [&cutoff](const BufferEntry &e) {
    return e.timestamp < cutoff;
  });

  float    window_peak[2]   = {};
  double   total_sum_sq[2]  = {};
  uint64_t total_frames     = 0;

  for (const auto &e : _window) {
    for (int c = 0; c < 2; c++) {
      if (e.peak[c] > window_peak[c]) window_peak[c] = e.peak[c];
      total_sum_sq[c] += e.sum_sq[c];
    }
    total_frames += e.frames;
  }

  _left_rms  = total_frames > 0
      ? static_cast<float>(std::sqrt(total_sum_sq[0] / total_frames)) : 0.0f;
  _right_rms = total_frames > 0
      ? static_cast<float>(std::sqrt(total_sum_sq[1] / total_frames)) : 0.0f;

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

void monitoring::Stream::destroy() {
  pw_stream_disconnect(_stream);
  pw_stream_destroy(_stream);
  _stream = nullptr;
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
