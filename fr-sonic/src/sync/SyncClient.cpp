#include "SyncClient.h"

#include "logging/log.h"

#include <algorithm>
#include <array>
#include <charconv>
#include <cstring>
#include <map>
#include <regex>
#include <string_view>

#include <pipewire/keys.h>
#include <pipewire/pipewire.h>
#include <spa/param/props.h>
#include <spa/pod/iter.h>
#include <spa/pod/parser.h>
#include <spa/utils/dict.h>

namespace {

constexpr auto SyncNodeName = "se.sync_master";
constexpr auto SyncRole = "sync-master";
constexpr size_t MaxBeatHistory = 1024;

spa_pod *pod_body(const spa_pod *pod) {
  return reinterpret_cast<spa_pod *>(reinterpret_cast<uintptr_t>(pod) +
                                    sizeof(spa_pod));
}

std::vector<sesync::BeatScheduleEntry> parse_schedule(const std::string &json) {
  static const std::regex pair_regex(R"(\[\s*([0-9]+)\s*,\s*([0-9]+)\s*\])");
  std::vector<sesync::BeatScheduleEntry> result;

  for (auto it = std::sregex_iterator(json.begin(), json.end(), pair_regex);
       it != std::sregex_iterator(); ++it) {
    result.push_back({.beat = std::stoull((*it)[1].str()),
                      .nsec = std::stoull((*it)[2].str())});
  }

  std::ranges::sort(result, {}, &sesync::BeatScheduleEntry::beat);
  return result;
}

uint64_t parse_u64_prop(const char *value) {
  if (value == nullptr)
    return 0;

  uint64_t result = 0;
  const auto *end = value + std::strlen(value);
  const auto parsed = std::from_chars(value, end, result);
  if (parsed.ec != std::errc{} || parsed.ptr != end)
    return 0;
  return result;
}

std::vector<sesync::BpmEntry> parse_bpm(const std::string &json) {
  static const std::regex bpm_regex(
      R"(\[\s*([0-9]+)\s*,\s*([0-9]+(?:\.[0-9]+)?)\s*\])");
  std::vector<sesync::BpmEntry> result;

  const auto bpm_pos = json.find("\"bpm\"");
  if (bpm_pos == std::string::npos)
    return result;
  const auto state_pos = json.find("\"transport_state\"", bpm_pos);
  const auto bpm_json = json.substr(bpm_pos, state_pos - bpm_pos);

  for (auto it =
           std::sregex_iterator(bpm_json.begin(), bpm_json.end(), bpm_regex);
       it != std::sregex_iterator(); ++it) {
    result.push_back({.beat = std::stoull((*it)[1].str()),
                      .bpm = std::stod((*it)[2].str())});
  }

  std::ranges::sort(result, {}, &sesync::BpmEntry::beat);
  return result;
}

sesync::TransportState parse_transport_state(const std::string_view value) {
  if (value == "playing")
    return sesync::TransportState::Playing;
  if (value == "start_scheduled")
    return sesync::TransportState::StartScheduled;
  return sesync::TransportState::Stopped;
}

std::vector<sesync::TransportStateEntry>
parse_transport_states(const std::string &json) {
  static const std::regex state_regex(
      R"STATE(\[\s*([0-9]+)\s*,\s*"([^"]+)"\s*\])STATE");
  std::vector<sesync::TransportStateEntry> result;

  const auto state_pos = json.find("\"transport_state\"");
  if (state_pos == std::string::npos)
    return result;
  const auto state_json = json.substr(state_pos);

  for (auto it = std::sregex_iterator(state_json.begin(), state_json.end(),
                                      state_regex);
       it != std::sregex_iterator(); ++it) {
    result.push_back(
        {.beat = std::stoull((*it)[1].str()),
         .state = parse_transport_state((*it)[2].str())});
  }

  std::ranges::sort(result, {}, &sesync::TransportStateEntry::beat);
  return result;
}

std::vector<sesync::BeatScheduleEntry>
merge_history(const std::vector<sesync::BeatScheduleEntry> &old_history,
              const std::vector<sesync::BeatScheduleEntry> &schedule) {
  std::map<uint64_t, uint64_t> by_beat;
  for (const auto &entry : old_history)
    by_beat[entry.beat] = entry.nsec;
  for (const auto &entry : schedule)
    by_beat[entry.beat] = entry.nsec;

  while (by_beat.size() > MaxBeatHistory)
    by_beat.erase(by_beat.begin());

  std::vector<sesync::BeatScheduleEntry> result;
  result.reserve(by_beat.size());
  for (const auto &[beat, nsec] : by_beat)
    result.push_back({.beat = beat, .nsec = nsec});
  return result;
}

const pw_registry_events registry_events = {
    .version = PW_VERSION_REGISTRY_EVENTS,
    .global = sesync::SyncClient::registry_global,
    .global_remove = sesync::SyncClient::registry_global_remove,
};

const pw_node_events node_events = {
    .version = PW_VERSION_NODE_EVENTS,
    .param = sesync::SyncClient::node_param,
};

} // namespace

std::optional<sesync::BeatScheduleEntry>
sesync::SyncSnapshot::current_beat(const uint64_t now_nsec) const {
  std::optional<BeatScheduleEntry> current;
  for (const auto &entry : beat_history) {
    if (entry.nsec > now_nsec)
      break;
    current = entry;
  }
  return current;
}

sesync::TransportState
sesync::SyncSnapshot::transport_state_at(const uint64_t beat) const {
  auto state = TransportState::Stopped;
  for (const auto &entry : transport_states) {
    if (entry.beat > beat)
      break;
    state = entry.state;
  }
  return state;
}

double sesync::SyncSnapshot::bpm_at(const uint64_t beat) const {
  auto value = bpm.empty() ? 0.0 : bpm.front().bpm;
  for (const auto &entry : bpm) {
    if (entry.beat > beat)
      break;
    value = entry.bpm;
  }
  return value;
}

sesync::SyncClient::SyncClient(pw_core *core, pw_loop *loop)
    : _core(core), _loop(loop) {}

sesync::SyncClient::~SyncClient() { stop(); }

void sesync::SyncClient::start() {
  if (_registry != nullptr)
    return;

  logging::log<logging::LogLevel::Info>("Starting sync client");
  _registry = pw_core_get_registry(_core, PW_VERSION_REGISTRY, 0);
  if (_registry == nullptr) {
    logging::log<logging::LogLevel::Error>(
        "Failed to create sync client registry");
    return;
  }

  pw_registry_add_listener(_registry, &_registry_listener, &registry_events,
                           this);
}

void sesync::SyncClient::stop() {
  destroy_master();

  if (_registry != nullptr) {
    spa_hook_remove(&_registry_listener);
    pw_proxy_destroy(reinterpret_cast<pw_proxy *>(_registry));
    _registry = nullptr;
  }

  std::atomic_store(&_snapshot, SnapshotPtr{});
}

sesync::SyncClient::SnapshotPtr sesync::SyncClient::snapshot() const {
  return std::atomic_load(&_snapshot);
}

std::optional<sesync::BeatScheduleEntry>
sesync::SyncClient::current_beat(const uint64_t now_nsec) const {
  const auto current = snapshot();
  if (!current)
    return std::nullopt;
  return current->current_beat(now_nsec);
}

void sesync::SyncClient::set_snapshot_callback(SnapshotCallback callback) {
  _snapshot_callback = std::move(callback);
}

void sesync::SyncClient::handle_global(const uint32_t id, const char *type,
                                       const uint32_t version,
                                       const spa_dict *props) {
  if (type == nullptr || std::strcmp(type, PW_TYPE_INTERFACE_Node) != 0)
    return;

  const auto *node_name = spa_dict_lookup(props, PW_KEY_NODE_NAME);
  const auto *role = spa_dict_lookup(props, "se.role");
  const auto serial = parse_u64_prop(spa_dict_lookup(props, "object.serial"));
  if ((node_name != nullptr && std::strcmp(node_name, SyncNodeName) == 0) ||
      (role != nullptr && std::strcmp(role, SyncRole) == 0)) {
    bind_master(id, version, serial);
  }
}

void sesync::SyncClient::handle_global_remove(const uint32_t id) {
  if (id != _master_object_id)
    return;

  logging::log<logging::LogLevel::Info>(
      "Sync client master node removed: {}", id);
  destroy_master();
  std::atomic_store(&_snapshot, SnapshotPtr{});
}

void sesync::SyncClient::bind_master(const uint32_t id, const uint32_t version,
                                     const uint64_t serial) {
  if (_master_node != nullptr) {
    if (_master_object_id == id)
      return;

    if (serial <= _master_object_serial) {
      logging::log<logging::LogLevel::Warning>(
          "Sync client found older sync master node {} serial {}; keeping {} "
          "serial {}",
          id, serial, _master_object_id, _master_object_serial);
      return;
    }

    logging::log<logging::LogLevel::Info>(
        "Sync client switching sync master from node {} serial {} to node {} "
        "serial {}",
        _master_object_id, _master_object_serial, id, serial);
    destroy_master();
    std::atomic_store(&_snapshot, SnapshotPtr{});
  }

  _master_node = static_cast<pw_node *>(pw_registry_bind(
      _registry, id, PW_TYPE_INTERFACE_Node,
      std::min(version, static_cast<uint32_t>(PW_VERSION_NODE)),
      0));
  if (_master_node == nullptr) {
    logging::log<logging::LogLevel::Error>(
        "Sync client failed to bind master node {}", id);
    return;
  }

  _master_object_id = id;
  _master_object_serial = serial;
  logging::log<logging::LogLevel::Info>(
      "Sync client bound master node {} serial {}", _master_object_id,
      _master_object_serial);
  subscribe_master_params();
}

void sesync::SyncClient::destroy_master() {
  if (_master_node == nullptr)
    return;

  spa_hook_remove(&_node_listener);
  pw_proxy_destroy(reinterpret_cast<pw_proxy *>(_master_node));
  _master_node = nullptr;
  _master_object_id = 0;
  _master_object_serial = 0;
}

void sesync::SyncClient::subscribe_master_params() {
  if (_master_node == nullptr)
    return;

  pw_node_add_listener(_master_node, &_node_listener, &node_events, this);
  std::array<uint32_t, 1> param_ids = {SPA_PARAM_Props};
  pw_node_subscribe_params(_master_node, param_ids.data(), param_ids.size());
  pw_node_enum_params(_master_node, 0, SPA_PARAM_Props, 0, 0, nullptr);
}

void sesync::SyncClient::handle_param(const uint32_t id, const spa_pod *pod) {
  if (id != SPA_PARAM_Props || pod == nullptr ||
      SPA_POD_TYPE(pod) != SPA_TYPE_Object)
    return;

  const auto params_prop = spa_pod_find_prop(pod, nullptr, SPA_PROP_params);
  if (params_prop == nullptr || params_prop->value.type != SPA_TYPE_Struct)
    return;

  std::optional<std::string> schedule_json;
  std::optional<std::string> params_json;

  const char *key = nullptr;
  uint32_t index = 0;
  spa_pod *child = nullptr;
  SPA_POD_FOREACH(pod_body(&params_prop->value),
                  SPA_POD_BODY_SIZE(&params_prop->value), child) {
    if (index % 2 == 0) {
      key = nullptr;
      if (child->type == SPA_TYPE_String)
        spa_pod_get_string(child, &key);
    } else if (key != nullptr && child->type == SPA_TYPE_String) {
      const char *value = nullptr;
      spa_pod_get_string(child, &value);
      if (value != nullptr && std::string_view(key) == "beat.schedule")
        schedule_json = value;
      else if (value != nullptr && std::string_view(key) == "beat.params")
        params_json = value;
    }
    ++index;
  }

  if (!schedule_json || !params_json)
    return;

  apply_params(*schedule_json, *params_json);
}

void sesync::SyncClient::apply_params(const std::string &schedule_json,
                                      const std::string &params_json) {
  auto next = std::make_shared<SyncSnapshot>();
  next->master_object_id = _master_object_id;
  next->beat_schedule = parse_schedule(schedule_json);
  next->bpm = parse_bpm(params_json);
  next->transport_states = parse_transport_states(params_json);

  const auto previous = snapshot();
  next->beat_history =
      merge_history(previous ? previous->beat_history
                             : std::vector<BeatScheduleEntry>{},
                    next->beat_schedule);

  std::atomic_store(&_snapshot,
                    std::static_pointer_cast<const SyncSnapshot>(next));

  logging::log<logging::LogLevel::Debug>(
      "Sync client updated snapshot: {} scheduled beats, {} bpm entries, {} "
      "transport entries",
      next->beat_schedule.size(), next->bpm.size(),
      next->transport_states.size());

  if (_snapshot_callback)
    _snapshot_callback(next);
}

void sesync::SyncClient::registry_global(void *data, const uint32_t id,
                                         uint32_t permissions,
                                         const char *type,
                                         const uint32_t version,
                                         const spa_dict *props) {
  (void)permissions;
  static_cast<SyncClient *>(data)->handle_global(id, type, version, props);
}

void sesync::SyncClient::registry_global_remove(void *data, const uint32_t id) {
  static_cast<SyncClient *>(data)->handle_global_remove(id);
}

void sesync::SyncClient::node_param(void *data, int seq, const uint32_t id,
                                    uint32_t index, uint32_t next,
                                    const spa_pod *param) {
  (void)seq;
  (void)index;
  (void)next;
  static_cast<SyncClient *>(data)->handle_param(id, param);
}
