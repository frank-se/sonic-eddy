#include "MidiManipulator.h"

#include "logging/log.h"

#include <algorithm>
#include <cstring>
#include <format>
#include <pipewire/keys.h>
#include <pipewire/properties.h>
#include <regex>
#include <spa/control/control.h>
#include <spa/param/format.h>
#include <spa/param/props.h>
#include <spa/pod/builder.h>
#include <spa/pod/iter.h>
#include <spa/utils/type.h>

namespace {

const pw_stream_events capture_stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .param_changed = midi::MidiManipulator::capture_param_changed_callback,
    .process = midi::MidiManipulator::capture_process_callback,
};

const pw_stream_events playback_stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .param_changed = midi::MidiManipulator::playback_param_changed_callback,
    .process = midi::MidiManipulator::playback_process_callback,
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

bool is_channel_voice_status(const uint8_t status) {
  const auto high = status & 0xf0;
  return high >= 0x80 && high <= 0xe0;
}

} // namespace

midi::MidiManipulatorRules::MidiManipulatorRules() {
  for (uint8_t channel = 0; channel < channel_map.size(); ++channel)
    channel_map[channel] = channel;
}

midi::MidiManipulator::MidiManipulator(pw_loop *loop,
                                       MidiManipulatorConfig config)
    : _loop(loop), _config(std::move(config)) {}

midi::MidiManipulator::~MidiManipulator() { stop(); }

bool midi::MidiManipulator::start() {
  if (_capture_stream != nullptr || _playback_stream != nullptr)
    return true;

  logging::log<logging::LogLevel::Info>("Starting MIDI manipulator '{}'",
                                        _config.name);
  if (!setup_capture_stream() || !setup_playback_stream()) {
    stop();
    return false;
  }

  return true;
}

void midi::MidiManipulator::stop() {
  if (_capture_stream != nullptr) {
    pw_stream_destroy(_capture_stream);
    _capture_stream = nullptr;
  }

  if (_playback_stream != nullptr) {
    pw_stream_destroy(_playback_stream);
    _playback_stream = nullptr;
  }
}

bool midi::MidiManipulator::setup_capture_stream() {
  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Midi", PW_KEY_MEDIA_CATEGORY, "Capture",
      PW_KEY_MEDIA_ROLE, "DSP", PW_KEY_MEDIA_CLASS, "Stream/Input/Midi",
      PW_KEY_FORMAT_DSP, "8 bit raw midi", PW_KEY_NODE_NAME,
      std::format("{}.capture", _config.name).c_str(), PW_KEY_NODE_DESCRIPTION,
      std::format("{} capture", _config.description).c_str(), "se.role",
      "midi-manipulator-capture", "se.midi_manipulator.name",
      _config.name.c_str(), "pmx.purpose", "midi-manipulator-capture",
      "node.linger", "true", "node.passive", "true", nullptr);
  if (!_config.tag.empty())
    pw_properties_set(properties, "pmx.tag", _config.tag.c_str());

  const auto stream_name = std::format("{}.capture", _config.name);
  _capture_stream = pw_stream_new_simple(_loop, stream_name.c_str(), properties,
                                         &capture_stream_events, this);
  if (_capture_stream == nullptr)
    return false;

  const spa_pod *params[1];
  std::array<uint8_t, 256> buffer{};
  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, buffer.data(), buffer.size());
  params[0] = build_control_format(builder);

  const auto result = pw_stream_connect(
      _capture_stream, PW_DIRECTION_INPUT, PW_ID_ANY,
      static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS |
                                   PW_STREAM_FLAG_RT_PROCESS),
      params, 1);
  if (result < 0)
    return false;

  publish_params();
  return true;
}

bool midi::MidiManipulator::setup_playback_stream() {
  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Midi", PW_KEY_MEDIA_CATEGORY, "Playback",
      PW_KEY_MEDIA_ROLE, "DSP", PW_KEY_MEDIA_CLASS, "Stream/Output/Midi",
      PW_KEY_FORMAT_DSP, "8 bit raw midi", PW_KEY_NODE_NAME,
      std::format("{}.playback", _config.name).c_str(), PW_KEY_NODE_DESCRIPTION,
      std::format("{} playback", _config.description).c_str(), "se.role",
      "midi-manipulator-playback", "se.midi_manipulator.name",
      _config.name.c_str(), "pmx.purpose", "midi-manipulator-playback",
      "node.linger", "true", "node.passive", "true", nullptr);
  if (!_config.tag.empty())
    pw_properties_set(properties, "pmx.tag", _config.tag.c_str());

  const auto stream_name = std::format("{}.playback", _config.name);
  _playback_stream = pw_stream_new_simple(
      _loop, stream_name.c_str(), properties, &playback_stream_events, this);
  if (_playback_stream == nullptr)
    return false;

  const spa_pod *params[1];
  std::array<uint8_t, 256> buffer{};
  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, buffer.data(), buffer.size());
  params[0] = build_control_format(builder);

  const auto result = pw_stream_connect(
      _playback_stream, PW_DIRECTION_OUTPUT, PW_ID_ANY,
      static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS |
                                   PW_STREAM_FLAG_RT_PROCESS),
      params, 1);
  return result >= 0;
}

void midi::MidiManipulator::process_capture() {
  auto *pipewire_buffer = pw_stream_dequeue_buffer(_capture_stream);
  if (pipewire_buffer == nullptr)
    return;

  auto *buffer = pipewire_buffer->buffer;
  if (buffer->n_datas != 1 || buffer->datas[0].data == nullptr ||
      buffer->datas[0].chunk == nullptr) {
    pw_stream_queue_buffer(_capture_stream, pipewire_buffer);
    return;
  }

  auto *pod = static_cast<spa_pod *>(spa_pod_from_data(
      buffer->datas[0].data, buffer->datas[0].maxsize,
      buffer->datas[0].chunk->offset, buffer->datas[0].chunk->size));

  std::vector<MidiEvent> events;
  if (pod != nullptr && spa_pod_is_sequence(pod)) {
    auto *sequence = reinterpret_cast<spa_pod_sequence *>(pod);
    spa_pod_control *pod_control = nullptr;
    SPA_POD_SEQUENCE_FOREACH(sequence, pod_control) {
      if (pod_control->type != SPA_CONTROL_Midi &&
          pod_control->type != SPA_CONTROL_UMP)
        continue;

      const auto *data =
          static_cast<const uint8_t *>(SPA_POD_BODY(&pod_control->value));
      const auto length = SPA_POD_BODY_SIZE(&pod_control->value);
      MidiEvent event{
          .offset = pod_control->offset,
          .type = pod_control->type,
          .data = std::vector<uint8_t>(data, data + length),
      };
      if (transform_event(event))
        events.push_back(std::move(event));
    }
  }

  {
    std::scoped_lock lock{_events_mutex};
    _events = std::move(events);
  }

  pw_stream_queue_buffer(_capture_stream, pipewire_buffer);
}

void midi::MidiManipulator::process_playback() {
  auto *pipewire_buffer = pw_stream_dequeue_buffer(_playback_stream);
  if (pipewire_buffer == nullptr)
    return;

  auto *buffer = pipewire_buffer->buffer;
  if (buffer->n_datas != 1 || buffer->datas[0].data == nullptr ||
      buffer->datas[0].chunk == nullptr) {
    pw_stream_queue_buffer(_playback_stream, pipewire_buffer);
    return;
  }

  std::vector<MidiEvent> events;
  {
    std::scoped_lock lock{_events_mutex};
    events.swap(_events);
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
  for (const auto &event : events) {
    spa_pod_builder_control(&builder, event.offset, event.type);
    spa_pod_builder_bytes(&builder, event.data.data(), event.data.size());
  }
  spa_pod_builder_pop(&builder, &sequence_frame);
  spa_data->chunk->size = builder.state.offset;

  pw_stream_queue_buffer(_playback_stream, pipewire_buffer);
}

bool midi::MidiManipulator::transform_event(MidiEvent &event) const {
  if (event.type == SPA_CONTROL_Midi)
    return transform_midi_bytes(event.data);
  if (event.type == SPA_CONTROL_UMP)
    return transform_ump_bytes(event.data);
  return true;
}

bool midi::MidiManipulator::transform_midi_bytes(
    std::vector<uint8_t> &bytes) const {
  if (bytes.empty() || !is_channel_voice_status(bytes[0]))
    return true;

  const auto channel = static_cast<uint8_t>(bytes[0] & 0x0f);
  if (_rules.drop_channels[channel])
    return false;

  bytes[0] = static_cast<uint8_t>((bytes[0] & 0xf0) |
                                  (_rules.channel_map[channel] & 0x0f));
  return true;
}

bool midi::MidiManipulator::transform_ump_bytes(
    std::vector<uint8_t> &bytes) const {
  if (bytes.size() < sizeof(uint32_t))
    return true;

  uint32_t word = 0;
  std::memcpy(&word, bytes.data(), sizeof(uint32_t));
  const auto status = static_cast<uint8_t>((word >> 16) & 0xff);
  if (!is_channel_voice_status(status))
    return true;

  const auto channel = static_cast<uint8_t>(status & 0x0f);
  if (_rules.drop_channels[channel])
    return false;

  const auto mapped_status = static_cast<uint8_t>(
      (status & 0xf0) | (_rules.channel_map[channel] & 0x0f));
  word = (word & 0xff00ffffu) | (static_cast<uint32_t>(mapped_status) << 16);
  std::memcpy(bytes.data(), &word, sizeof(uint32_t));
  return true;
}

void midi::MidiManipulator::publish_params() {
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
  spa_pod_builder_string(&builder, "midi.router.config");
  spa_pod_builder_string(
      &builder, R"({"version":1,"drop_channels":[],"channel_map":[]})");
  spa_pod_builder_pop(&builder, &struct_frame);

  const spa_pod *params[] = {
      static_cast<spa_pod *>(spa_pod_builder_pop(&builder, &object_frame))};
  pw_stream_update_params(_capture_stream, params, 1);
}

void midi::MidiManipulator::handle_params(const uint32_t id,
                                          const spa_pod *param) {
  if (param == nullptr || id != SPA_PARAM_Props)
    return;

  const auto params_prop = spa_pod_find_prop(param, nullptr, SPA_PROP_params);
  if (params_prop == nullptr || params_prop->value.type != SPA_TYPE_Struct)
    return;

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

void midi::MidiManipulator::handle_param_value(const char *key,
                                               const spa_pod *value) {
  if (std::strcmp(key, "midi.router.config") != 0 ||
      value->type != SPA_TYPE_String)
    return;

  const char *config = nullptr;
  spa_pod_get_string(value, &config);
  if (config != nullptr)
    parse_config(config);
}

void midi::MidiManipulator::parse_config(const char *config_json) {
  MidiManipulatorRules rules{};
  const std::string_view config{config_json};
  const std::regex drop_regex{R"("drop_channels"\s*:\s*\[([^\]]*)\])"};
  const std::regex map_regex{R"(\[\s*(\d+)\s*,\s*(\d+)\s*\])"};

  std::cmatch drop_match;
  if (std::regex_search(config_json, drop_match, drop_regex)) {
    const std::string drop_list = drop_match[1].str();
    const std::regex number_regex{R"(\d+)"};
    for (auto it = std::sregex_iterator(drop_list.begin(), drop_list.end(),
                                        number_regex);
         it != std::sregex_iterator(); ++it) {
      const auto channel = std::clamp(std::stoi((*it).str()), 1, 16) - 1;
      rules.drop_channels[static_cast<size_t>(channel)] = true;
    }
  }

  for (auto it = std::cregex_iterator(config.begin(), config.end(), map_regex);
       it != std::cregex_iterator(); ++it) {
    const auto from = std::clamp(std::stoi((*it)[1].str()), 1, 16) - 1;
    const auto to = std::clamp(std::stoi((*it)[2].str()), 1, 16) - 1;
    rules.channel_map[static_cast<size_t>(from)] = static_cast<uint8_t>(to);
  }

  _rules = rules;
  logging::log<logging::LogLevel::Info>("MIDI manipulator '{}' updated config",
                                        _config.name);
}

void midi::MidiManipulator::capture_process_callback(void *data) {
  static_cast<MidiManipulator *>(data)->process_capture();
}

void midi::MidiManipulator::playback_process_callback(void *data) {
  static_cast<MidiManipulator *>(data)->process_playback();
}

void midi::MidiManipulator::capture_param_changed_callback(
    void *data, const uint32_t id, const spa_pod *param) {
  static_cast<MidiManipulator *>(data)->handle_params(id, param);
}

void midi::MidiManipulator::playback_param_changed_callback(
    void *data, const uint32_t id, const spa_pod *param) {
  static_cast<MidiManipulator *>(data)->handle_params(id, param);
}
