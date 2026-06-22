#include "Ducker.h"

#include "logging/log.h"

#include <algorithm>
#include <cstring>
#include <format>

#include <pipewire/keys.h>
#include <pipewire/properties.h>
#include <spa/control/control.h>
#include <spa/param/audio/format-utils.h>
#include <spa/param/format.h>
#include <spa/param/props.h>
#include <spa/pod/builder.h>
#include <spa/pod/iter.h>

namespace {

const pw_stream_events midi_capture_stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .process = ducker::Ducker::midi_capture_process_callback,
};

const pw_stream_events audio_capture_stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .param_changed = ducker::Ducker::audio_capture_param_changed_callback,
    .process = ducker::Ducker::audio_capture_process_callback,
};

const pw_stream_events audio_playback_stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .param_changed = ducker::Ducker::audio_playback_param_changed_callback,
    .process = ducker::Ducker::audio_playback_process_callback,
};

const spa_pod *build_control_format(spa_pod_builder &builder) {
  spa_pod_frame frame{};
  spa_pod_builder_push_object(&builder, &frame, SPA_TYPE_OBJECT_Format,
                              SPA_PARAM_EnumFormat);
  spa_pod_builder_add(
      &builder, SPA_FORMAT_mediaType, SPA_POD_Id(SPA_MEDIA_TYPE_application),
      SPA_FORMAT_mediaSubtype, SPA_POD_Id(SPA_MEDIA_SUBTYPE_control), 0);
  return static_cast<const spa_pod *>(spa_pod_builder_pop(&builder, &frame));
}

const spa_pod *build_audio_format(spa_pod_builder &builder) {
  spa_pod_frame frame{};
  spa_pod_builder_push_object(&builder, &frame, SPA_TYPE_OBJECT_Format,
                              SPA_PARAM_EnumFormat);
  spa_pod_builder_add(
      &builder, SPA_FORMAT_mediaType, SPA_POD_Id(SPA_MEDIA_TYPE_audio),
      SPA_FORMAT_mediaSubtype, SPA_POD_Id(SPA_MEDIA_SUBTYPE_raw),
      SPA_FORMAT_AUDIO_format,
      SPA_POD_Id(SPA_AUDIO_FORMAT_F32),
      0);
  return static_cast<const spa_pod *>(spa_pod_builder_pop(&builder, &frame));
}

bool parse_midi_note_on(const uint8_t *data, size_t len, uint8_t &note,
                        uint8_t &velocity) {
  if (len < 3) return false;
  const auto status = data[0] & 0xf0;
  if (status != 0x90) return false;
  note = data[1] & 0x7f;
  velocity = data[2] & 0x7f;
  return velocity > 0; /* velocity 0 = note-off */
}

bool parse_midi_note_off(const uint8_t *data, size_t len) {
  if (len < 1) return false;
  const auto status = data[0] & 0xf0;
  if (status == 0x80) return true;
  if (status == 0x90 && len >= 3 && data[2] == 0) return true;
  return false;
}

} // namespace

ducker::Ducker::Ducker(pw_loop *loop, DuckerConfig config)
    : _loop(loop), _config(std::move(config)) {}

ducker::Ducker::~Ducker() { stop(); }

bool ducker::Ducker::start() {
  if (_midi_capture_stream || _audio_capture_stream || _audio_playback_stream)
    return true;

  logging::log<logging::LogLevel::Info>("Starting ducker '{}'", _config.name);

  if (!setup_midi_capture_stream() || !setup_audio_capture_stream() ||
      !setup_audio_playback_stream()) {
    stop();
    return false;
  }

  return true;
}

void ducker::Ducker::stop() {
  if (_midi_capture_stream) {
    pw_stream_destroy(_midi_capture_stream);
    _midi_capture_stream = nullptr;
  }
  if (_audio_capture_stream) {
    pw_stream_destroy(_audio_capture_stream);
    _audio_capture_stream = nullptr;
  }
  if (_audio_playback_stream) {
    pw_stream_destroy(_audio_playback_stream);
    _audio_playback_stream = nullptr;
  }
}

bool ducker::Ducker::setup_midi_capture_stream() {
  auto *props = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Midi",
      PW_KEY_MEDIA_CATEGORY, "Capture",
      PW_KEY_MEDIA_ROLE, "DSP",
      PW_KEY_MEDIA_CLASS, "Stream/Input/Midi",
      PW_KEY_FORMAT_DSP, "8 bit raw midi",
      PW_KEY_NODE_NAME, std::format("{}.midi-capture", _config.name).c_str(),
      PW_KEY_NODE_DESCRIPTION,
      std::format("{} midi capture", _config.description).c_str(),
      "se.role", "ducker-midi-capture",
      "pmx.purpose", "ducker-midi",
      "node.linger", "true",
      "node.passive", "true",
      nullptr);
  if (!_config.tag.empty())
    pw_properties_set(props, "pmx.tag", _config.tag.c_str());

  _midi_capture_stream = pw_stream_new_simple(
      _loop, std::format("{}.midi-capture", _config.name).c_str(), props,
      &midi_capture_stream_events, this);
  if (!_midi_capture_stream) return false;

  std::array<uint8_t, 256> buf{};
  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, buf.data(), buf.size());
  const spa_pod *params[] = {build_control_format(builder)};

  return pw_stream_connect(
             _midi_capture_stream, PW_DIRECTION_INPUT, PW_ID_ANY,
             static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS |
                                          PW_STREAM_FLAG_RT_PROCESS),
             params, 1) >= 0;
}

bool ducker::Ducker::setup_audio_capture_stream() {
  auto *props = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Audio",
      PW_KEY_MEDIA_CATEGORY, "Capture",
      PW_KEY_MEDIA_ROLE, "DSP",
      PW_KEY_NODE_NAME, std::format("{}.capture", _config.name).c_str(),
      PW_KEY_NODE_DESCRIPTION,
      std::format("{} capture", _config.description).c_str(),
      "se.role", "ducker-capture",
      "pmx.purpose", "ducker-capture",
      "node.linger", "true",
      "node.passive", "true",
      nullptr);
  if (!_config.tag.empty())
    pw_properties_set(props, "pmx.tag", _config.tag.c_str());
  if (!_config.capture_target_object.empty())
    pw_properties_set(props, PW_KEY_TARGET_OBJECT,
                      _config.capture_target_object.c_str());

  _audio_capture_stream = pw_stream_new_simple(
      _loop, std::format("{}.capture", _config.name).c_str(), props,
      &audio_capture_stream_events, this);
  if (!_audio_capture_stream) return false;

  std::array<uint8_t, 256> buf{};
  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, buf.data(), buf.size());
  const spa_pod *params[] = {build_audio_format(builder)};

  auto capture_flags = static_cast<pw_stream_flags>(
      PW_STREAM_FLAG_MAP_BUFFERS | PW_STREAM_FLAG_RT_PROCESS |
      PW_STREAM_FLAG_ASYNC);
  if (!_config.capture_target_object.empty())
    capture_flags =
        static_cast<pw_stream_flags>(capture_flags | PW_STREAM_FLAG_AUTOCONNECT);
  return pw_stream_connect(_audio_capture_stream, PW_DIRECTION_INPUT, PW_ID_ANY,
                           capture_flags, params, 1) >= 0;
}

bool ducker::Ducker::setup_audio_playback_stream() {
  auto *props = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Audio",
      PW_KEY_MEDIA_CATEGORY, "Playback",
      PW_KEY_MEDIA_ROLE, "DSP",
      PW_KEY_NODE_NAME, std::format("{}.playback", _config.name).c_str(),
      PW_KEY_NODE_DESCRIPTION,
      std::format("{} playback", _config.description).c_str(),
      "se.role", "ducker-playback",
      "pmx.purpose", "ducker-playback",
      "node.linger", "true",
      "node.passive", "true",
      nullptr);
  if (!_config.tag.empty())
    pw_properties_set(props, "pmx.tag", _config.tag.c_str());
  if (!_config.playback_target_object.empty())
    pw_properties_set(props, PW_KEY_TARGET_OBJECT,
                      _config.playback_target_object.c_str());

  _audio_playback_stream = pw_stream_new_simple(
      _loop, std::format("{}.playback", _config.name).c_str(), props,
      &audio_playback_stream_events, this);
  if (!_audio_playback_stream) return false;

  std::array<uint8_t, 256> buf{};
  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, buf.data(), buf.size());
  const spa_pod *params[] = {build_audio_format(builder)};

  auto playback_flags = static_cast<pw_stream_flags>(
      PW_STREAM_FLAG_MAP_BUFFERS | PW_STREAM_FLAG_RT_PROCESS |
      PW_STREAM_FLAG_TRIGGER);
  if (!_config.playback_target_object.empty())
    playback_flags = static_cast<pw_stream_flags>(playback_flags |
                                                   PW_STREAM_FLAG_AUTOCONNECT);
  return pw_stream_connect(_audio_playback_stream, PW_DIRECTION_OUTPUT,
                           PW_ID_ANY, playback_flags, params, 1) >= 0;
}

void ducker::Ducker::process_midi_capture() {
  auto *pw_buf = pw_stream_dequeue_buffer(_midi_capture_stream);
  if (!pw_buf) return;

  auto *buf = pw_buf->buffer;
  if (buf->n_datas == 1 && buf->datas[0].data && buf->datas[0].chunk) {
    auto *pod = static_cast<spa_pod *>(spa_pod_from_data(
        buf->datas[0].data, buf->datas[0].maxsize,
        buf->datas[0].chunk->offset, buf->datas[0].chunk->size));

    if (pod && spa_pod_is_sequence(pod)) {
      auto *seq = reinterpret_cast<spa_pod_sequence *>(pod);
      spa_pod_control *ctrl = nullptr;
      const int ch = _param_midi_channel.load(std::memory_order_relaxed);
      SPA_POD_SEQUENCE_FOREACH(seq, ctrl) {
        if (ctrl->type != SPA_CONTROL_Midi) continue;
        const auto *data =
            static_cast<const uint8_t *>(SPA_POD_BODY(&ctrl->value));
        const auto len = SPA_POD_BODY_SIZE(&ctrl->value);
        if (len < 1 || (data[0] & 0x0f) != ch) continue;

        uint8_t note = 0, velocity = 0;
        if (parse_midi_note_on(data, len, note, velocity)) {
          _midi_events.push({true, note, velocity});
        } else if (parse_midi_note_off(data, len)) {
          _midi_events.push({false, 0, 0});
        }
      }
    }
  }

  pw_stream_queue_buffer(_midi_capture_stream, pw_buf);
}

void ducker::Ducker::process_audio_capture() {
  if (_audio_playback_stream == nullptr) return;

  const auto result = pw_stream_trigger_process(_audio_playback_stream);
  if (result >= 0) return;

  while (_audio_capture_stream != nullptr) {
    auto *buf = pw_stream_dequeue_buffer(_audio_capture_stream);
    if (!buf) break;
    pw_stream_queue_buffer(_audio_capture_stream, buf);
  }
}

void ducker::Ducker::process_audio_playback() {
  MidiNoteEvent evt{};
  while (_midi_events.pop(evt)) {
    if (evt.note_on) on_note_on(evt.note, evt.velocity);
    else on_note_off();
  }

  pw_buffer *capture_buf = nullptr;
  if (_audio_capture_stream != nullptr) {
    while (true) {
      auto *next = pw_stream_dequeue_buffer(_audio_capture_stream);
      if (!next) break;
      if (capture_buf)
        pw_stream_queue_buffer(_audio_capture_stream, capture_buf);
      capture_buf = next;
    }
  }

  auto *playback_buf = pw_stream_dequeue_buffer(_audio_playback_stream);

  if (capture_buf && playback_buf) {
    auto *cap_spa = capture_buf->buffer;
    auto *play_spa = playback_buf->buffer;

    if (cap_spa->n_datas > 0 && cap_spa->datas[0].data &&
        cap_spa->datas[0].chunk && play_spa->n_datas > 0 &&
        play_spa->datas[0].data && play_spa->datas[0].chunk) {
      const auto *cap_data = &cap_spa->datas[0];
      auto *play_data = &play_spa->datas[0];
      const auto channels = std::max(_audio_format.channels, 2u);
      const auto bytes_per_frame = channels * sizeof(float);
      const auto cap_frames =
          static_cast<uint32_t>(cap_data->chunk->size / bytes_per_frame);
      const auto offset_samples = cap_data->chunk->offset / sizeof(float);
      const auto *samples =
          static_cast<const float *>(cap_data->data) + offset_samples;

      const auto requested = playback_buf->requested;
      const auto writable_frames =
          requested > 0
              ? std::min<uint32_t>(
                    static_cast<uint32_t>(play_data->maxsize / bytes_per_frame),
                    static_cast<uint32_t>(requested))
              : static_cast<uint32_t>(play_data->maxsize / bytes_per_frame);

      const auto output_frames = std::min(cap_frames, writable_frames);
      auto *output = static_cast<float *>(play_data->data);
      const bool bypass = _param_bypass.load(std::memory_order_relaxed);

      for (uint32_t f = 0; f < output_frames; ++f) {
        const float gain = bypass ? 1.0f : advance_envelope(1);
        for (uint32_t c = 0; c < channels; ++c)
          output[f * channels + c] = samples[f * channels + c] * gain;
      }
      for (uint32_t f = output_frames; f < writable_frames; ++f) {
        for (uint32_t c = 0; c < channels; ++c)
          output[f * channels + c] = 0.0f;
      }

      play_data->chunk->offset = 0;
      play_data->chunk->size =
          static_cast<uint32_t>(writable_frames * bytes_per_frame);
      play_data->chunk->stride = static_cast<int32_t>(bytes_per_frame);
    }
  }

  if (capture_buf)
    pw_stream_queue_buffer(_audio_capture_stream, capture_buf);
  if (playback_buf)
    pw_stream_queue_buffer(_audio_playback_stream, playback_buf);
}

void ducker::Ducker::on_note_on(const uint8_t note, const uint8_t velocity) {
  const float depth = _param_depth.load(std::memory_order_relaxed);
  const float attack = _param_attack.load(std::memory_order_relaxed);
  const uint32_t rate = std::max(_audio_format.rate, 1u);

  _applied_depth = (static_cast<float>(127 - note) / 127.0f) * depth;
  const float effective_attack =
      attack * (1.0f - static_cast<float>(velocity) / 127.0f);

  _start_gain = _current_gain;
  _target_gain = 1.0f - _applied_depth;
  _phase = DuckerPhase::Attack;
  _phase_t = 0.0f;
  const float duration_samples =
      std::max(effective_attack * static_cast<float>(rate), 1.0f);
  _phase_step = 1.0f / duration_samples;
}

void ducker::Ducker::on_note_off() {
  /* Fixed-length ducking: note length is ignored; hold fires automatically. */
}

float ducker::Ducker::advance_envelope(const uint32_t n_samples) {
  if (_phase == DuckerPhase::Idle)
    return _current_gain;

  const int shape = (_phase == DuckerPhase::Attack)
                        ? _param_attack_shape.load(std::memory_order_relaxed)
                        : _param_release_shape.load(std::memory_order_relaxed);

  _phase_t = std::min(_phase_t + _phase_step * static_cast<float>(n_samples),
                      1.0f);
  _current_gain = _start_gain + (_target_gain - _start_gain) * apply_curve(_phase_t, shape);

  if (_phase_t >= 1.0f) {
    _current_gain = _target_gain;
    const uint32_t rate = std::max(_audio_format.rate, 1u);
    if (_phase == DuckerPhase::Attack) {
      _start_gain = _target_gain;
      _phase = DuckerPhase::Hold;
      _phase_t = 0.0f;
      _phase_step = 1.0f / std::max(
          _param_hold.load(std::memory_order_relaxed) * static_cast<float>(rate), 1.0f);
    } else if (_phase == DuckerPhase::Hold) {
      _start_gain = _current_gain;
      _target_gain = 1.0f;
      _phase = DuckerPhase::Release;
      _phase_t = 0.0f;
      _phase_step = 1.0f / std::max(
          _param_release.load(std::memory_order_relaxed) * static_cast<float>(rate), 1.0f);
    } else {
      _phase = DuckerPhase::Idle;
    }
  }

  return _current_gain;
}

float ducker::Ducker::apply_curve(const float t, const int shape) {
  switch (shape) {
  case 1: return t * t;                          /* exponential: slow start, fast end */
  case 2: return 1.0f - (1.0f - t) * (1.0f - t); /* logarithmic: fast start, slow end */
  default: return t;                             /* linear */
  }
}

void ducker::Ducker::handle_audio_format(const uint32_t id,
                                         const spa_pod *param) {
  handle_audio_params(id, param);
  if (!param || id != SPA_PARAM_Format) return;

  uint32_t media_type = 0, media_subtype = 0;
  if (spa_format_parse(param, &media_type, &media_subtype) < 0) return;
  if (media_type != SPA_MEDIA_TYPE_audio ||
      media_subtype != SPA_MEDIA_SUBTYPE_raw)
    return;

  spa_format_audio_raw_parse(param, &_audio_format);
  publish_params();
}

void ducker::Ducker::handle_audio_params(const uint32_t id,
                                          const spa_pod *param) {
  if (!param || id != SPA_PARAM_Props) return;

  const auto *params_prop =
      spa_pod_find_prop(param, nullptr, SPA_PROP_params);
  if (!params_prop || params_prop->value.type != SPA_TYPE_Struct) return;

  const char *key = nullptr;
  uint32_t index = 0;
  spa_pod *child = nullptr;
  SPA_POD_FOREACH(static_cast<spa_pod *>(SPA_POD_BODY(&params_prop->value)),
                  SPA_POD_BODY_SIZE(&params_prop->value), child) {
    if (index % 2 == 0) {
      key = nullptr;
      if (child->type == SPA_TYPE_String)
        spa_pod_get_string(child, &key);
    } else if (key) {
      handle_param_value(key, child);
    }
    ++index;
  }
}

void ducker::Ducker::handle_param_value(const char *key,
                                         const spa_pod *value) {
  float f = 0.0f;
  int32_t i = 0;

  if (std::strcmp(key, "attack") == 0 && spa_pod_get_float(value, &f) == 0)
    _param_attack.store(std::max(f, 0.0f), std::memory_order_relaxed);
  else if (std::strcmp(key, "hold") == 0 && spa_pod_get_float(value, &f) == 0)
    _param_hold.store(std::max(f, 0.0f), std::memory_order_relaxed);
  else if (std::strcmp(key, "release") == 0 && spa_pod_get_float(value, &f) == 0)
    _param_release.store(std::max(f, 0.0f), std::memory_order_relaxed);
  else if (std::strcmp(key, "depth") == 0 && spa_pod_get_float(value, &f) == 0)
    _param_depth.store(std::clamp(f, 0.0f, 1.0f), std::memory_order_relaxed);
  else if (std::strcmp(key, "attack_shape") == 0 && spa_pod_get_int(value, &i) == 0)
    _param_attack_shape.store(std::clamp(i, 0, 2), std::memory_order_relaxed);
  else if (std::strcmp(key, "release_shape") == 0 && spa_pod_get_int(value, &i) == 0)
    _param_release_shape.store(std::clamp(i, 0, 2), std::memory_order_relaxed);
  else if (std::strcmp(key, "bypass") == 0) {
    spa_pod_get_int(value, &i);
    _param_bypass.store(i != 0, std::memory_order_relaxed);
  } else if (std::strcmp(key, "midi_channel") == 0 &&
             spa_pod_get_int(value, &i) == 0)
    _param_midi_channel.store(std::clamp(i, 0, 15), std::memory_order_relaxed);
}

void ducker::Ducker::publish_params() {
  if (!_audio_capture_stream) return;

  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, _params_buffer.data(), _params_buffer.size());

  spa_pod_frame object_frame{};
  spa_pod_builder_push_object(&builder, &object_frame, SPA_TYPE_OBJECT_Props,
                              SPA_PARAM_Props);
  spa_pod_builder_prop(&builder, SPA_PROP_params, 0);

  spa_pod_frame struct_frame{};
  spa_pod_builder_push_struct(&builder, &struct_frame);

  auto add_float = [&](const char *k, float v) {
    spa_pod_builder_string(&builder, k);
    spa_pod_builder_float(&builder, v);
  };
  auto add_int = [&](const char *k, int32_t v) {
    spa_pod_builder_string(&builder, k);
    spa_pod_builder_int(&builder, v);
  };

  add_float("attack", _param_attack.load(std::memory_order_relaxed));
  add_float("hold", _param_hold.load(std::memory_order_relaxed));
  add_float("release", _param_release.load(std::memory_order_relaxed));
  add_float("depth", _param_depth.load(std::memory_order_relaxed));
  add_int("attack_shape", _param_attack_shape.load(std::memory_order_relaxed));
  add_int("release_shape",
          _param_release_shape.load(std::memory_order_relaxed));
  add_int("bypass",
          _param_bypass.load(std::memory_order_relaxed) ? 1 : 0);
  add_int("midi_channel",
          _param_midi_channel.load(std::memory_order_relaxed));

  spa_pod_builder_pop(&builder, &struct_frame);

  const spa_pod *params[] = {
      static_cast<spa_pod *>(spa_pod_builder_pop(&builder, &object_frame))};
  pw_stream_update_params(_audio_capture_stream, params, 1);
}

/* Static callbacks */

void ducker::Ducker::midi_capture_process_callback(void *data) {
  static_cast<Ducker *>(data)->process_midi_capture();
}

void ducker::Ducker::audio_capture_process_callback(void *data) {
  static_cast<Ducker *>(data)->process_audio_capture();
}

void ducker::Ducker::audio_playback_process_callback(void *data) {
  static_cast<Ducker *>(data)->process_audio_playback();
}

void ducker::Ducker::audio_capture_param_changed_callback(void *data,
                                                           const uint32_t id,
                                                           const spa_pod *param) {
  static_cast<Ducker *>(data)->handle_audio_format(id, param);
}

void ducker::Ducker::audio_playback_param_changed_callback(void *data,
                                                            const uint32_t id,
                                                            const spa_pod *param) {
  static_cast<Ducker *>(data)->handle_audio_params(id, param);
}
