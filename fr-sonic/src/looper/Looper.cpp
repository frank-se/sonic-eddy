#include "Looper.h"

#include "logging/log.h"

#include <algorithm>
#include <array>
#include <charconv>
#include <cstring>
#include <format>
#include <optional>
#include <regex>
#include <sstream>
#include <string_view>
#include <utility>

#include <pipewire/keys.h>
#include <pipewire/properties.h>
#include <spa/param/props.h>
#include <spa/param/audio/format-utils.h>
#include <spa/pod/builder.h>
#include <spa/pod/iter.h>

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

constexpr uint32_t PassthroughMaxFrames = 16384;

std::string pod_type_name(const uint32_t type) {
  switch (type) {
  case SPA_TYPE_String:
    return "String";
  case SPA_TYPE_Float:
    return "Float";
  case SPA_TYPE_Double:
    return "Double";
  case SPA_TYPE_Int:
    return "Int";
  case SPA_TYPE_Long:
    return "Long";
  case SPA_TYPE_Struct:
    return "Struct";
  default:
    return std::format("{}", type);
  }
}

std::optional<uint64_t> parse_u64(std::string_view text) {
  while (!text.empty() && text.front() == ' ')
    text.remove_prefix(1);
  while (!text.empty() && text.back() == ' ')
    text.remove_suffix(1);
  uint64_t value = 0;
  const auto result =
      std::from_chars(text.data(), text.data() + text.size(), value);
  if (result.ec != std::errc{} || result.ptr != text.data() + text.size())
    return std::nullopt;
  return value;
}

std::optional<looper::CommandEvent>
parse_command_text(uint64_t scheduled_beat, std::string_view text) {
  std::istringstream stream{std::string{text}};
  std::string command;
  stream >> command;
  if (command.empty())
    return std::nullopt;

  looper::CommandEvent event{.scheduled_beat = scheduled_beat};
  if (command == "play") {
    uint32_t loop_number = 0;
    if (!(stream >> loop_number))
      return std::nullopt;
    event.kind = looper::CommandKind::Play;
    event.loop_number = loop_number;
    return event;
  }

  if (command == "stop") {
    event.kind = looper::CommandKind::Stop;
    return event;
  }

  if (command == "archive") {
    uint32_t loop_number = 0;
    if (!(stream >> loop_number))
      return std::nullopt;
    event.kind = looper::CommandKind::Archive;
    event.loop_number = loop_number;
    return event;
  }

  if (command == "cut") {
    uint64_t first = 0;
    uint64_t second = 0;
    uint32_t loop_number = 0;
    if (!(stream >> first >> second))
      return std::nullopt;
    if (stream >> loop_number) {
      event.kind = looper::CommandKind::CutRange;
      event.start_beat = first;
      event.end_beat = second;
      event.loop_number = loop_number;
    } else {
      event.kind = looper::CommandKind::CutLength;
      event.loop_length = first;
      event.loop_number = static_cast<uint32_t>(second);
    }
    return event;
  }

  return std::nullopt;
}

} // namespace

looper::Looper::Looper(pw_loop *loop, LooperConfig config)
    : _loop(loop), _config(std::move(config)),
      _mix(std::clamp(_config.mix, 0.0f, 1.0f)) {}

looper::Looper::~Looper() { stop(); }

bool looper::Looper::start() {
  if (_capture_stream != nullptr || _playback_stream != nullptr)
    return true;

  logging::log<logging::LogLevel::Info>("Starting looper '{}'",
                                        _config.name);
  _passthrough_channels = std::max(_config.channels, 1u);
  _passthrough_buffer.resize(PassthroughMaxFrames * _passthrough_channels);
  _passthrough_frames = 0;

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

  publish_params();
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
  drain_command_events();

  if (_capture_stream != nullptr) {
    auto *capture_buffer = pw_stream_dequeue_buffer(_capture_stream);
    if (capture_buffer != nullptr) {
      capture_passthrough_input(capture_buffer);
      pw_stream_queue_buffer(_capture_stream, capture_buffer);
    }
  }

  if (_playback_stream == nullptr)
    return;

  auto *playback_buffer = pw_stream_dequeue_buffer(_playback_stream);
  if (playback_buffer == nullptr)
    return;

  write_passthrough_output(playback_buffer);
  pw_stream_queue_buffer(_playback_stream, playback_buffer);
}

void looper::Looper::drain_command_events() {
  CommandEvent event{};
  while (_command_events.pop(event)) {
    ++_processed_command_count;
    logging::log<logging::LogLevel::Info>(
        "Looper '{}' processed command event kind={} beat={} loop={}",
        _config.name, static_cast<uint32_t>(event.kind), event.scheduled_beat,
        event.loop_number);
  }
}

void looper::Looper::publish_params() {
  if (_capture_stream == nullptr)
    return;

  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, _params_buffer.data(), _params_buffer.size());

  spa_pod_frame object_frame{};
  spa_pod_builder_push_object(&builder, &object_frame, SPA_TYPE_OBJECT_Props,
                              SPA_PARAM_Props);
  spa_pod_builder_prop(&builder, SPA_PROP_params, 0);

  spa_pod_frame struct_frame{};
  spa_pod_builder_push_struct(&builder, &struct_frame);
  spa_pod_builder_string(&builder, "mix");
  spa_pod_builder_float(&builder, _mix.load(std::memory_order_relaxed));
  spa_pod_builder_string(&builder, "commands");
  spa_pod_builder_string(&builder, "[]");
  spa_pod_builder_pop(&builder, &struct_frame);

  const spa_pod *params[] = {
      static_cast<spa_pod *>(spa_pod_builder_pop(&builder, &object_frame))};
  pw_stream_update_params(_capture_stream, params, 1);
}

void looper::Looper::capture_passthrough_input(pw_buffer *capture_buffer) {
  auto *buffer = capture_buffer->buffer;
  if (buffer->n_datas == 0 || buffer->datas[0].data == nullptr ||
      buffer->datas[0].chunk == nullptr) {
    _passthrough_frames = 0;
    return;
  }

  const auto *data = &buffer->datas[0];
  const auto channels = std::max(active_format().channels, 1u);
  const auto bytes_per_frame = channels * sizeof(float);
  const auto frames = std::min(
      static_cast<uint32_t>(data->chunk->size / bytes_per_frame),
      PassthroughMaxFrames);
  const auto sample_count = frames * channels;
  const auto *samples = static_cast<const float *>(data->data);
  const auto offset_samples = data->chunk->offset / sizeof(float);

  if (channels == _passthrough_channels &&
      sample_count <= _passthrough_buffer.size()) {
    std::copy_n(samples + offset_samples, sample_count,
                _passthrough_buffer.data());
  } else {
    _passthrough_frames = 0;
    return;
  }

  _passthrough_frames = frames;
}

void looper::Looper::write_passthrough_output(pw_buffer *playback_buffer) {
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
    const auto writable_frames =
        static_cast<uint32_t>(size / bytes_per_frame);
    const auto copy_frames = std::min(writable_frames, _passthrough_frames);
    const auto copy_size =
        static_cast<uint32_t>(copy_frames * bytes_per_frame);

    auto *samples = static_cast<uint8_t *>(data->data);
    const auto dry_gain = 1.0f - _mix.load(std::memory_order_relaxed);
    auto *output = reinterpret_cast<float *>(samples);
    bool copied_dry_signal = false;
    if (copy_size > 0 && _passthrough_channels == channels) {
      const auto sample_count = copy_frames * channels;
      for (uint32_t sample = 0; sample < sample_count; ++sample)
        output[sample] = _passthrough_buffer[sample] * dry_gain;
      copied_dry_signal = true;
    }
    if (!copied_dry_signal) {
      std::memset(samples, 0, size);
    } else if (copy_size < size) {
      std::memset(samples + copy_size, 0, size - copy_size);
    }
    data->chunk->offset = 0;
    data->chunk->size = size;
    data->chunk->stride = static_cast<int32_t>(bytes_per_frame);
  }
}

void looper::Looper::handle_capture_format(const uint32_t id,
                                           const spa_pod *param) {
  handle_params(id, param);
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
  handle_params(id, param);
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

void looper::Looper::handle_params(const uint32_t id, const spa_pod *param) {
  if (param == nullptr || id != SPA_PARAM_Props)
    return;

  const auto params_prop = spa_pod_find_prop(param, nullptr, SPA_PROP_params);
  if (params_prop == nullptr) {
    logging::log<logging::LogLevel::Warning>(
        "Looper '{}' received Props update without SPA_PROP_params",
        _config.name);
    return;
  }

  if (params_prop->value.type != SPA_TYPE_Struct) {
    logging::log<logging::LogLevel::Warning>(
        "Looper '{}' expected SPA_PROP_params Struct, got {}", _config.name,
        pod_type_name(params_prop->value.type));
    return;
  }

  const char *key = nullptr;
  uint32_t index = 0;
  spa_pod *child = nullptr;
  SPA_POD_FOREACH(static_cast<spa_pod *>(SPA_POD_BODY(&params_prop->value)),
                  SPA_POD_BODY_SIZE(&params_prop->value), child) {
    if (index % 2 == 0) {
      key = nullptr;
      if (child->type == SPA_TYPE_String)
        spa_pod_get_string(child, &key);
    } else if (key != nullptr) {
      handle_param_value(key, child);
    }
    ++index;
  }

  publish_params();
}

void looper::Looper::handle_param_value(const char *key,
                                        const spa_pod *value) {
  if (std::strcmp(key, "mix") == 0) {
    float mix = _mix.load(std::memory_order_relaxed);
    if (value->type == SPA_TYPE_Float) {
      spa_pod_get_float(value, &mix);
    } else if (value->type == SPA_TYPE_Double) {
      double double_mix = 0.0;
      spa_pod_get_double(value, &double_mix);
      mix = static_cast<float>(double_mix);
    } else if (value->type == SPA_TYPE_Int) {
      int32_t int_mix = 0;
      spa_pod_get_int(value, &int_mix);
      mix = static_cast<float>(int_mix);
    } else {
      logging::log<logging::LogLevel::Warning>(
          "Looper '{}' ignored mix param with type {}", _config.name,
          pod_type_name(value->type));
      return;
    }

    _mix.store(std::clamp(mix, 0.0f, 1.0f), std::memory_order_relaxed);
    logging::log<logging::LogLevel::Info>("Looper '{}' mix set to {}",
                                          _config.name,
                                          _mix.load(std::memory_order_relaxed));
    return;
  }

  if (std::strcmp(key, "commands") == 0 || std::strcmp(key, "command") == 0) {
    if (value->type != SPA_TYPE_String) {
      logging::log<logging::LogLevel::Warning>(
          "Looper '{}' ignored commands param with type {}", _config.name,
          pod_type_name(value->type));
      return;
    }
    const char *commands = nullptr;
    spa_pod_get_string(value, &commands);
    if (commands != nullptr)
      parse_commands_param(commands);
  }
}

void looper::Looper::enqueue_command(const CommandEvent &event) {
  if (_command_events.push(event))
    return;

  ++_dropped_command_count;
  logging::log<logging::LogLevel::Error>(
      "Looper '{}' command event queue full; dropped command kind={} beat={} "
      "loop={} dropped_count={}",
      _config.name, static_cast<uint32_t>(event.kind), event.scheduled_beat,
      event.loop_number, _dropped_command_count);
}

void looper::Looper::parse_commands_param(const char *value) {
  const std::string_view text{value};
  static const std::regex tuple_regex{
      R"re(\[\s*([0-9]+)\s*,\s*"([^"]+)"\s*\])re"};

  bool parsed_tuple = false;
  std::vector<std::pair<uint64_t, std::string>> seen_commands;
  for (auto it = std::cregex_iterator(value, value + text.size(), tuple_regex);
       it != std::cregex_iterator{}; ++it) {
    parsed_tuple = true;
    const auto beat = parse_u64((*it)[1].str());
    if (!beat) {
      logging::log<logging::LogLevel::Warning>(
          "Looper '{}' ignored command with invalid beat '{}'", _config.name,
          (*it)[1].str());
      continue;
    }
    const auto command_text = (*it)[2].str();
    const auto duplicate =
        std::ranges::any_of(seen_commands, [&](const auto &seen) {
          return seen.first == *beat && seen.second == command_text;
        });
    if (duplicate) {
      logging::log<logging::LogLevel::Error>(
          "Looper '{}' ignored duplicate command '{}' for beat {}",
          _config.name, command_text, *beat);
      continue;
    }
    seen_commands.emplace_back(*beat, command_text);

    auto event = parse_command_text(*beat, command_text);
    if (event)
      enqueue_command(*event);
    else
      logging::log<logging::LogLevel::Warning>(
          "Looper '{}' ignored invalid command '{}'", _config.name,
          command_text);
  }

  if (parsed_tuple)
    return;

  auto event = parse_command_text(0, text);
  if (event)
    enqueue_command(*event);
  else
    logging::log<logging::LogLevel::Warning>(
        "Looper '{}' ignored invalid commands param '{}'", _config.name, value);
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
