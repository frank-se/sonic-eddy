#include "SyncMaster.h"

#include "logging/log.h"
#include "spa/debug/pod.h"

#include <algorithm>
#include <cerrno>
#include <cmath>
#include <cstddef>
#include <ctime>
#include <format>
#include <regex>
#include <string_view>

#include <pipewire/core.h>
#include <pipewire/extensions/client-node.h>
#include <pipewire/proxy.h>
#include <pipewire/keys.h>
#include <pipewire/node.h>
#include <pipewire/properties.h>
#include <spa/param/props.h>
#include <spa/pod/builder.h>
#include <spa/pod/iter.h>
#include <spa/pod/parser.h>
#include <spa/utils/defs.h>

namespace {

constexpr auto SyncNodeName = "se.sync_master";

uint64_t timespec_to_nsec(const timespec &ts) {
  return static_cast<uint64_t>(ts.tv_sec) * 1'000'000'000ull +
         static_cast<uint64_t>(ts.tv_nsec);
}

spa_pod *pod_body(const spa_pod *pod) {
  return reinterpret_cast<spa_pod *>(reinterpret_cast<uintptr_t>(pod) +
                                     sizeof(spa_pod));
}

const char *pod_type_name(const uint32_t type) {
  switch (type) {
  case SPA_TYPE_None:
    return "None";
  case SPA_TYPE_Bool:
    return "Bool";
  case SPA_TYPE_Id:
    return "Id";
  case SPA_TYPE_Int:
    return "Int";
  case SPA_TYPE_Long:
    return "Long";
  case SPA_TYPE_Float:
    return "Float";
  case SPA_TYPE_Double:
    return "Double";
  case SPA_TYPE_String:
    return "String";
  case SPA_TYPE_Bytes:
    return "Bytes";
  case SPA_TYPE_Rectangle:
    return "Rectangle";
  case SPA_TYPE_Fraction:
    return "Fraction";
  case SPA_TYPE_Bitmap:
    return "Bitmap";
  case SPA_TYPE_Array:
    return "Array";
  case SPA_TYPE_Struct:
    return "Struct";
  case SPA_TYPE_Object:
    return "Object";
  case SPA_TYPE_Sequence:
    return "Sequence";
  case SPA_TYPE_Pointer:
    return "Pointer";
  case SPA_TYPE_Fd:
    return "Fd";
  case SPA_TYPE_Choice:
    return "Choice";
  case SPA_TYPE_Pod:
    return "Pod";
  default:
    return "Unknown";
  }
}

const pw_client_node_events client_node_events = {
    .version = PW_VERSION_CLIENT_NODE_EVENTS,
    .set_param = sesync::SyncMaster::set_param,
};

} // namespace

sesync::SyncMaster::SyncMaster(pw_core *core, pw_loop *loop,
                               SyncMasterConfig config)
    : _core(core), _loop(loop), _config(config) {
  _config.lookahead_beats = std::max(_config.lookahead_beats, 1u);
  _config.slide_after_beats =
      std::clamp(_config.slide_after_beats, 1u, _config.lookahead_beats);
  if (_config.bpm <= 0.0)
    _config.bpm = 120.0;

  _schedule.reserve(_config.lookahead_beats);
}

sesync::SyncMaster::~SyncMaster() { stop(); }

void sesync::SyncMaster::start() {
  if (_client_node != nullptr)
    return;

  logging::log<logging::LogLevel::Info>("Starting sync master");

  setup_node_info();
  _anchor_nsec = now_nsec();
  _anchor_beat = 0;
  _bpm_beat = 0;
  _transport_state_beat = 0;
  recompute_schedule();

  auto *properties =
      pw_properties_new(PW_KEY_NODE_NAME, SyncNodeName, PW_KEY_NODE_DESCRIPTION,
                        "Sonic Eddy sync master", "media.class", "Midi/Bridge",
                        "se.role", "sync-master", nullptr);

  _client_node = static_cast<pw_client_node *>(
      pw_core_create_object(_core, "client-node", PW_TYPE_INTERFACE_ClientNode,
                            PW_VERSION_CLIENT_NODE, &properties->dict, 0));
  pw_properties_free(properties);

  if (_client_node == nullptr) {
    logging::log<logging::LogLevel::Error>(
        "Failed to create PipeWire sync master client-node");
    return;
  }

  pw_client_node_add_listener(_client_node, &_client_node_listener,
                              &client_node_events, this);

  _node = pw_client_node_get_node(_client_node, PW_VERSION_NODE, 0);
  if (_node == nullptr) {
    logging::log<logging::LogLevel::Warning>(
        "Sync master client-node did not return a node proxy");
  }

  publish_update();

  _timer_queue = pw_timer_queue_new(_loop);
  if (_timer_queue == nullptr) {
    logging::log<logging::LogLevel::Error>(
        "Failed to create sync master timer queue");
    return;
  }

  schedule_next_timer();
}

void sesync::SyncMaster::stop() {
  if (_timer_queue != nullptr) {
    pw_timer_queue_cancel(&_timer);
    pw_timer_queue_destroy(_timer_queue);
    _timer_queue = nullptr;
  }

  if (_client_node != nullptr) {
    logging::log<logging::LogLevel::Info>("Stopping sync master");
    spa_hook_remove(&_client_node_listener);
    pw_proxy_destroy(reinterpret_cast<pw_proxy *>(_client_node));
    _client_node = nullptr;
    _node = nullptr;
  }
}

void sesync::SyncMaster::setup_node_info() {
  _params[0] = SPA_PARAM_INFO(SPA_PARAM_Props, SPA_PARAM_INFO_READWRITE);
  _info = SPA_NODE_INFO_INIT();
  _info.max_input_ports = 0;
  _info.max_output_ports = 0;
  _info.change_mask = SPA_NODE_CHANGE_MASK_FLAGS | SPA_NODE_CHANGE_MASK_PARAMS;
  _info.flags = 0;
  _info.params = _params.data();
  _info.n_params = static_cast<uint32_t>(_params.size());
}

uint64_t sesync::SyncMaster::current_beat_now() const {
  return current_beat(now_nsec());
}

uint64_t sesync::SyncMaster::now_nsec() const {
  timespec ts{};
  clock_gettime(CLOCK_MONOTONIC, &ts);
  return timespec_to_nsec(ts);
}

uint64_t sesync::SyncMaster::beat_period_nsec() const {
  return beat_period_nsec(_config.bpm);
}

uint64_t sesync::SyncMaster::beat_period_nsec(const double bpm) const {
  return static_cast<uint64_t>(std::llround(60.0 * 1'000'000'000.0 / bpm));
}

uint64_t sesync::SyncMaster::current_beat(const uint64_t now) const {
  const auto period = beat_period_nsec();
  if (now <= _anchor_nsec)
    return _anchor_beat;

  return _anchor_beat + ((now - _anchor_nsec) / period);
}

uint64_t sesync::SyncMaster::nsec_for_beat(const uint64_t beat) const {
  if (_pending_bpm_beat && _pending_bpm && beat > *_pending_bpm_beat) {
    const auto boundary_nsec =
        _anchor_nsec +
        ((*_pending_bpm_beat - _anchor_beat) * beat_period_nsec());
    return boundary_nsec +
           ((beat - *_pending_bpm_beat) * beat_period_nsec(*_pending_bpm));
  }

  return _anchor_nsec + ((beat - _anchor_beat) * beat_period_nsec());
}

bool sesync::SyncMaster::accepts_change(const uint64_t beat,
                                        const uint64_t current) const {
  if (_transport_state == "stopped")
    return true;

  return beat >= current + _config.lead_time_beats;
}

void sesync::SyncMaster::activate_due_changes(const uint64_t now) {
  if (_pending_bpm_beat && _pending_bpm) {
    const auto pending_nsec =
        _anchor_nsec +
        ((*_pending_bpm_beat - _anchor_beat) * beat_period_nsec());
    if (now >= pending_nsec) {
      _anchor_beat = *_pending_bpm_beat;
      _anchor_nsec = pending_nsec;
      _bpm_beat = *_pending_bpm_beat;
      _config.bpm = *_pending_bpm;
      _pending_bpm_beat.reset();
      _pending_bpm.reset();
    }
  }

  if (_pending_transport_state_beat && _pending_transport_state) {
    const auto current = current_beat(now);
    if (current >= *_pending_transport_state_beat) {
      _transport_state_beat = *_pending_transport_state_beat;
      _transport_state = *_pending_transport_state;
      _pending_transport_state_beat.reset();
      _pending_transport_state.reset();
    }
  }
}

void sesync::SyncMaster::recompute_schedule() {
  const auto now = now_nsec();
  activate_due_changes(now);
  const auto current = current_beat(now);

  _schedule.clear();
  for (uint32_t i = 0; i < _config.lookahead_beats; ++i) {
    const auto beat = current + 1 + i;
    _schedule.push_back(BeatEntry{.beat = beat, .nsec = nsec_for_beat(beat)});
  }

  rebuild_json();
}

void sesync::SyncMaster::rebuild_json() {
  _schedule_json = "[";
  for (size_t i = 0; i < _schedule.size(); ++i) {
    if (i > 0)
      _schedule_json += ",";
    _schedule_json +=
        std::format("[{},{}]", _schedule[i].beat, _schedule[i].nsec);
  }
  _schedule_json += "]";

  _params_json =
      std::format(R"({{"bpm":[[{}, {:.6f}])", _bpm_beat, _config.bpm);
  if (_pending_bpm_beat && _pending_bpm) {
    _params_json +=
        std::format(R"(,[{}, {:.6f}])", *_pending_bpm_beat, *_pending_bpm);
  }
  _params_json += R"(],"transport_state":[)";
  _params_json +=
      std::format(R"([{},"{}"])", _transport_state_beat, _transport_state);
  if (_pending_transport_state_beat && _pending_transport_state) {
    _params_json += std::format(R"(,[{},"{}"])", *_pending_transport_state_beat,
                                *_pending_transport_state);
  }
  _params_json += "]}";
}

spa_pod *sesync::SyncMaster::build_props_pod() {
  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, _param_buffer.data(), _param_buffer.size());

  spa_pod_frame object_frame{};
  spa_pod_builder_push_object(&builder, &object_frame, SPA_TYPE_OBJECT_Props,
                              SPA_PARAM_Props);
  spa_pod_builder_prop(&builder, SPA_PROP_params, 0);

  spa_pod_frame struct_frame{};
  spa_pod_builder_push_struct(&builder, &struct_frame);
  spa_pod_builder_string(&builder, "beat.schedule");
  spa_pod_builder_string(&builder, _schedule_json.c_str());
  spa_pod_builder_string(&builder, "beat.params");
  spa_pod_builder_string(&builder, _params_json.c_str());
  spa_pod_builder_pop(&builder, &struct_frame);

  return static_cast<spa_pod *>(spa_pod_builder_pop(&builder, &object_frame));
}

void sesync::SyncMaster::publish_update() {
  if (_client_node == nullptr)
    return;

  ++_param_serial;
  _params[0].flags = SPA_PARAM_INFO_READWRITE;
  if ((_param_serial % 2) != 0)
    _params[0].flags |= SPA_PARAM_INFO_SERIAL;
  _params[0].user = _param_serial;
  _params[0].seq = static_cast<int32_t>(_param_serial);
  _info.change_mask = SPA_NODE_CHANGE_MASK_FLAGS | SPA_NODE_CHANGE_MASK_PARAMS;

  const spa_pod *params[] = {build_props_pod()};

  const auto result = pw_client_node_update(
      _client_node, PW_CLIENT_NODE_UPDATE_PARAMS | PW_CLIENT_NODE_UPDATE_INFO,
      1, params, &_info);

  if (result < 0) {
    logging::log<logging::LogLevel::Error>(
        "Failed to publish sync master params: {}", result);
  }
}

void sesync::SyncMaster::schedule_next_timer() {
  if (_timer_queue == nullptr || _schedule.empty())
    return;

  const auto index =
      std::min<size_t>(_config.slide_after_beats - 1, _schedule.size() - 1);
  const auto now = now_nsec();
  const auto target = _schedule[index].nsec;
  const auto timeout =
      target > now ? static_cast<int64_t>(target - now) : int64_t{1};

  pw_timer_queue_cancel(&_timer);
  pw_timer_queue_add(_timer_queue, &_timer, nullptr, timeout, timer_callback,
                     this);
}

void sesync::SyncMaster::on_timer() {
  recompute_schedule();
  publish_update();
  schedule_next_timer();
}

void sesync::SyncMaster::handle_set_param(const uint32_t id, uint32_t flags,
                                          const spa_pod *param) {
  logging::log<logging::LogLevel::Info>(
      "Sync master set_param id={} flags={} param={}", id, flags,
      param == nullptr ? "null" : pod_type_name(param->type));

  if (id != SPA_PARAM_Props)
    return;

  if (param == nullptr) {
    logging::log<logging::LogLevel::Warning>(
        "Sync master received null Props param; the set-param command did not "
        "deliver a POD payload");
    return;
  }

  const auto params_prop = spa_pod_find_prop(param, nullptr, SPA_PROP_params);
  if (params_prop == nullptr) {
    logging::log<logging::LogLevel::Warning>(
        "Sync master received Props update without SPA_PROP_params");
    return;
  }

  logging::log<logging::LogLevel::Info>(
      "Sync master Props params type={} size={}",
      pod_type_name(params_prop->value.type), params_prop->value.size);

  if (params_prop->value.type != SPA_TYPE_Struct) {
    logging::log<logging::LogLevel::Warning>(
        "Sync master expected SPA_PROP_params Struct, got {}",
        pod_type_name(params_prop->value.type));
    return;
  }

  bool changed = false;
  const char *key = nullptr;
  uint32_t index = 0;
  spa_pod *child = nullptr;
  SPA_POD_FOREACH(pod_body(&params_prop->value),
                  SPA_POD_BODY_SIZE(&params_prop->value), child) {
    logging::log<logging::LogLevel::Trace>(
        "Sync master Props params child index={} type={} size={}", index,
        pod_type_name(child->type), child->size);

    if (index % 2 == 0) {
      key = nullptr;
      if (child->type == SPA_TYPE_String)
        spa_pod_get_string(child, &key);
      if (key == nullptr) {
        logging::log<logging::LogLevel::Warning>(
            "Sync master ignored params key at index {} with type {}", index,
            pod_type_name(child->type));
      }
    } else if (key != nullptr && child->type == SPA_TYPE_String) {
      const char *value = nullptr;
      spa_pod_get_string(child, &value);
      if (value != nullptr)
        changed = handle_params_value(key, value) || changed;
    } else if (key != nullptr) {
      logging::log<logging::LogLevel::Warning>(
          "Sync master ignored params value for '{}' with type {}", key,
          pod_type_name(child->type));
    }
    ++index;
  }

  if (!changed) {
    logging::log<logging::LogLevel::Info>(
        "Sync master Props update did not change beat params");
    return;
  }

  recompute_schedule();
  publish_update();
  schedule_next_timer();
}

bool sesync::SyncMaster::handle_params_value(const char *key,
                                             const char *value) {
  logging::log<logging::LogLevel::Info>("Sync master received params '{}'='{}'",
                                        key, value);

  if (std::string_view(key) == "beat.params") {
    return apply_beat_params_patch(value);
  }

  logging::log<logging::LogLevel::Debug>(
      "Sync master ignored unsupported params key '{}'", key);
  return false;
}

bool sesync::SyncMaster::apply_beat_params_patch(const std::string &json) {
  logging::log<logging::LogLevel::Trace>("Received update string: {}", json);
  static const std::regex bpm_regex(
      R"("bpm"\s*:\s*\[\s*\[\s*([0-9]+)\s*,\s*([0-9]+(?:\.[0-9]+)?)\s*\])");
  static const std::regex state_regex(
      R"STATE("transport_state"\s*:\s*\[\s*\[\s*([0-9]+)\s*,\s*"([^"]+)")STATE");

  bool changed = false;
  std::smatch match;
  if (std::regex_search(json, match, bpm_regex)) {
    changed = apply_bpm_change(std::stoull(match[1].str()),
                               std::stod(match[2].str())) ||
              changed;
  }

  if (std::regex_search(json, match, state_regex)) {
    changed = apply_transport_state_change(std::stoull(match[1].str()),
                                           match[2].str()) ||
              changed;
  }

  if (!changed) {
    logging::log<logging::LogLevel::Warning>(
        "Sync master did not find an applicable beat.params patch in '{}'",
        json);
  }

  return changed;
}

bool sesync::SyncMaster::apply_bpm_change(const uint64_t beat,
                                          const double bpm) {
  if (bpm <= 0.0) {
    logging::log<logging::LogLevel::Warning>(
        "Sync master rejected bpm change at beat {} with bpm {}", beat, bpm);
    return false;
  }

  const auto now = now_nsec();
  const auto current = current_beat(now);
  if (!accepts_change(beat, current)) {
    logging::log<logging::LogLevel::Warning>(
        "Sync master rejected bpm change at beat {}; current beat is {}, "
        "lead time is {}",
        beat, current, _config.lead_time_beats);
    return false;
  }

  if (beat <= current) {
    const auto current_nsec = nsec_for_beat(current);
    _anchor_beat = current;
    _anchor_nsec = current_nsec;
    _bpm_beat = current;
    _config.bpm = bpm;
    _pending_bpm_beat.reset();
    _pending_bpm.reset();
    logging::log<logging::LogLevel::Info>(
        "Sync master applied bpm {} at current beat {}", bpm, current);
    return true;
  }

  _pending_bpm_beat = beat;
  _pending_bpm = bpm;
  logging::log<logging::LogLevel::Info>(
      "Sync master scheduled bpm {} at beat {}", bpm, beat);
  return true;
}

bool sesync::SyncMaster::apply_transport_state_change(const uint64_t beat,
                                                      std::string state) {
  if (state != "stopped" && state != "start_scheduled" && state != "playing") {
    logging::log<logging::LogLevel::Warning>(
        "Sync master rejected unknown transport state '{}' at beat {}", state,
        beat);
    return false;
  }

  const auto now = now_nsec();
  const auto current = current_beat(now);
  if (!accepts_change(beat, current)) {
    logging::log<logging::LogLevel::Warning>(
        "Sync master rejected transport state '{}' at beat {}; current beat "
        "is {}, lead time is {}",
        state, beat, current, _config.lead_time_beats);
    return false;
  }

  if (beat <= current) {
    _transport_state_beat = current;
    _transport_state = std::move(state);
    _pending_transport_state_beat.reset();
    _pending_transport_state.reset();
    logging::log<logging::LogLevel::Info>(
        "Sync master applied transport state '{}' at current beat {}",
        _transport_state, current);
    return true;
  }

  _pending_transport_state_beat = beat;
  _pending_transport_state = std::move(state);
  logging::log<logging::LogLevel::Info>(
      "Sync master scheduled transport state '{}' at beat {}",
      *_pending_transport_state, beat);
  return true;
}

void sesync::SyncMaster::timer_callback(void *data) {
  static_cast<SyncMaster *>(data)->on_timer();
}

int sesync::SyncMaster::set_param(void *object, const uint32_t id,
                                  const uint32_t flags, const spa_pod *param) {
  auto *self = static_cast<SyncMaster *>(object);
  self->handle_set_param(id, flags, param);
  return 0;
}
