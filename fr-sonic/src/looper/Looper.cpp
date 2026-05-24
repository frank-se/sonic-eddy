#include "Looper.h"

#include "logging/log.h"

#include <algorithm>
#include <array>
#include <cstring>
#include <format>
#include <utility>

#include <pipewire/keys.h>
#include <pipewire/properties.h>
#include <spa/param/audio/format-utils.h>
#include <spa/pod/builder.h>

namespace {

const pw_stream_events capture_stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .param_changed = looper::Looper::capture_param_changed_callback,
    .process = looper::Looper::capture_process_callback,
};

const pw_stream_events playback_stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .param_changed = looper::Looper::playback_param_changed_callback,
    .process = looper::Looper::playback_process_callback,
};

} // namespace

looper::Looper::Looper(pw_loop *loop, LooperConfig config)
    : _loop(loop), _config(std::move(config)) {}

looper::Looper::~Looper() { stop(); }

bool looper::Looper::start() {
  if (_capture_stream != nullptr || _playback_stream != nullptr)
    return true;

  logging::log<logging::LogLevel::Info>("Starting looper '{}'",
                                        _config.name);
  if (!setup_capture_stream() || !setup_playback_stream()) {
    stop();
    return false;
  }

  return true;
}

void looper::Looper::stop() {
  if (_capture_stream != nullptr) {
    pw_stream_destroy(_capture_stream);
    _capture_stream = nullptr;
  }

  if (_playback_stream != nullptr) {
    pw_stream_destroy(_playback_stream);
    _playback_stream = nullptr;
  }
}

bool looper::Looper::setup_capture_stream() {
  const auto name = capture_name();
  const auto description = std::format("{} capture", _config.description);
  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Audio", PW_KEY_MEDIA_CATEGORY, "Capture",
      PW_KEY_MEDIA_ROLE, "DSP", PW_KEY_MEDIA_CLASS, "Stream/Input/Audio",
      PW_KEY_NODE_NAME, name.c_str(), PW_KEY_NODE_DESCRIPTION,
      description.c_str(), "se.role", "looper-capture", "se.looper.name",
      _config.name.c_str(), "pmx.purpose", "looper-capture", nullptr);
  if (!_config.tag.empty())
    pw_properties_set(properties, "pmx.tag", _config.tag.c_str());
  if (_config.capture_target_object)
    pw_properties_set(properties, PW_KEY_TARGET_OBJECT,
                      _config.capture_target_object->c_str());

  _capture_stream = pw_stream_new_simple(_loop, name.c_str(), properties,
                                         &capture_stream_events, this);
  if (_capture_stream == nullptr) {
    logging::log<logging::LogLevel::Error>(
        "Failed to create looper capture stream '{}'", name);
    return false;
  }

  const spa_pod *params[1];
  std::array<uint8_t, 1024> buffer{};
  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, buffer.data(), buffer.size());
  params[0] = build_audio_format(builder, _config.format, _config.channels);

  const auto result = pw_stream_connect(
      _capture_stream, PW_DIRECTION_INPUT, PW_ID_ANY,
      stream_flags(_config.capture_target_object.has_value()),
      params, 1);
  if (result < 0) {
    logging::log<logging::LogLevel::Error>(
        "Failed to connect looper capture stream '{}': {}", name, result);
    return false;
  }

  return true;
}

bool looper::Looper::setup_playback_stream() {
  const auto name = playback_name();
  const auto description = std::format("{} playback", _config.description);
  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Audio", PW_KEY_MEDIA_CATEGORY, "Playback",
      PW_KEY_MEDIA_ROLE, "DSP", PW_KEY_MEDIA_CLASS, "Stream/Output/Audio",
      PW_KEY_NODE_NAME, name.c_str(), PW_KEY_NODE_DESCRIPTION,
      description.c_str(), "se.role", "looper-playback", "se.looper.name",
      _config.name.c_str(), "pmx.purpose", "looper-playback", nullptr);
  if (!_config.tag.empty())
    pw_properties_set(properties, "pmx.tag", _config.tag.c_str());
  if (_config.playback_target_object)
    pw_properties_set(properties, PW_KEY_TARGET_OBJECT,
                      _config.playback_target_object->c_str());

  _playback_stream = pw_stream_new_simple(_loop, name.c_str(), properties,
                                          &playback_stream_events, this);
  if (_playback_stream == nullptr) {
    logging::log<logging::LogLevel::Error>(
        "Failed to create looper playback stream '{}'", name);
    return false;
  }

  const spa_pod *params[1];
  std::array<uint8_t, 1024> buffer{};
  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, buffer.data(), buffer.size());
  params[0] = build_audio_format(builder, _config.format, _config.channels);

  const auto result = pw_stream_connect(
      _playback_stream, PW_DIRECTION_OUTPUT, PW_ID_ANY,
      stream_flags(_config.playback_target_object.has_value()),
      params, 1);
  if (result < 0) {
    logging::log<logging::LogLevel::Error>(
        "Failed to connect looper playback stream '{}': {}", name, result);
    return false;
  }

  return true;
}

void looper::Looper::process() {
  if (_capture_stream != nullptr) {
    auto *capture_buffer = pw_stream_dequeue_buffer(_capture_stream);
    if (capture_buffer != nullptr)
      pw_stream_queue_buffer(_capture_stream, capture_buffer);
  }

  if (_playback_stream == nullptr)
    return;

  auto *playback_buffer = pw_stream_dequeue_buffer(_playback_stream);
  if (playback_buffer == nullptr)
    return;

  auto *buffer = playback_buffer->buffer;
  if (buffer->n_datas > 0 && buffer->datas[0].data != nullptr &&
      buffer->datas[0].chunk != nullptr) {
    auto *data = &buffer->datas[0];
    const auto frames =
        playback_buffer->requested == 0 ? uint32_t{0}
                                        : playback_buffer->requested;
    const auto channels = std::max(active_format().channels, 1u);
    const auto bytes_per_frame = channels * sizeof(float);
    const auto requested_size =
        static_cast<uint32_t>(frames * bytes_per_frame);
    const auto size = std::min<uint32_t>(data->maxsize, requested_size);

    std::memset(data->data, 0, size);
    data->chunk->offset = 0;
    data->chunk->size = size;
    data->chunk->stride = static_cast<int32_t>(bytes_per_frame);
  }

  pw_stream_queue_buffer(_playback_stream, playback_buffer);
}

void looper::Looper::handle_capture_format(const uint32_t id,
                                           const spa_pod *param) {
  if (param == nullptr || id != SPA_PARAM_Format)
    return;

  uint32_t media_type = 0;
  uint32_t media_subtype = 0;
  if (spa_format_parse(param, &media_type, &media_subtype) < 0)
    return;

  if (media_type != SPA_MEDIA_TYPE_audio ||
      media_subtype != SPA_MEDIA_SUBTYPE_raw)
    return;

  spa_format_audio_raw_parse(param, &_capture_format);
}

void looper::Looper::handle_playback_format(const uint32_t id,
                                            const spa_pod *param) {
  if (param == nullptr || id != SPA_PARAM_Format)
    return;

  uint32_t media_type = 0;
  uint32_t media_subtype = 0;
  if (spa_format_parse(param, &media_type, &media_subtype) < 0)
    return;

  if (media_type != SPA_MEDIA_TYPE_audio ||
      media_subtype != SPA_MEDIA_SUBTYPE_raw)
    return;

  spa_format_audio_raw_parse(param, &_playback_format);
}

std::string looper::Looper::capture_name() const {
  return std::format("{}.capture", _config.name);
}

std::string looper::Looper::playback_name() const {
  return std::format("{}.playback", _config.name);
}

const spa_audio_info_raw &looper::Looper::active_format() const {
  if (_playback_format.channels != 0)
    return _playback_format;
  if (_capture_format.channels != 0)
    return _capture_format;

  static const spa_audio_info_raw fallback = {
      .format = SPA_AUDIO_FORMAT_F32,
      .flags = SPA_AUDIO_FLAG_NONE,
      .rate = 48000,
      .channels = 2,
  };
  return fallback;
}

pw_stream_flags looper::Looper::stream_flags(const bool autoconnect) const {
  auto flags = static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS |
                                            PW_STREAM_FLAG_RT_PROCESS);
  if (autoconnect)
    flags = static_cast<pw_stream_flags>(flags | PW_STREAM_FLAG_AUTOCONNECT);
  return flags;
}

const spa_pod *looper::Looper::build_audio_format(spa_pod_builder &builder,
                                                  spa_audio_format format,
                                                  uint32_t channels) {
  spa_audio_info_raw audio_info{};
  audio_info.format = format;
  audio_info.channels = channels;
  return spa_format_audio_raw_build(&builder, SPA_PARAM_EnumFormat,
                                    &audio_info);
}

void looper::Looper::capture_process_callback(void *data) {
  static_cast<Looper *>(data)->process();
}

void looper::Looper::playback_process_callback(void *data) {
  static_cast<Looper *>(data)->process();
}

void looper::Looper::capture_param_changed_callback(void *data,
                                                    const uint32_t id,
                                                    const spa_pod *param) {
  static_cast<Looper *>(data)->handle_capture_format(id, param);
}

void looper::Looper::playback_param_changed_callback(void *data,
                                                     const uint32_t id,
                                                     const spa_pod *param) {
  static_cast<Looper *>(data)->handle_playback_format(id, param);
}
