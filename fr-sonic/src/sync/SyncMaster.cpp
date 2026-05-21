#include "SyncMaster.h"

#include "logging/log.h"

#include <algorithm>
#include <cerrno>
#include <cmath>
#include <ctime>
#include <format>

#include <pipewire/core.h>
#include <pipewire/extensions/client-node.h>
#include <pipewire/keys.h>
#include <pipewire/node.h>
#include <pipewire/properties.h>
#include <spa/param/props.h>
#include <spa/pod/builder.h>
#include <spa/pod/parser.h>
#include <spa/utils/defs.h>

namespace {

constexpr auto SyncNodeName = "se.sync_master";

uint64_t timespec_to_nsec(const timespec &ts) {
  return static_cast<uint64_t>(ts.tv_sec) * 1'000'000'000ull +
         static_cast<uint64_t>(ts.tv_nsec);
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
  recompute_schedule();

  auto *properties =
      pw_properties_new(PW_KEY_NODE_NAME, SyncNodeName, PW_KEY_NODE_DESCRIPTION,
                        "Sonic Eddy sync master", "media.class", "Midi/Bridge",
                        "se.role", "sync-master", nullptr);

  _client_node = static_cast<pw_client_node *>(pw_core_create_object(
      _core, "client-node", PW_TYPE_INTERFACE_ClientNode,
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
    pw_core_destroy(_core, _client_node);
    _client_node = nullptr;
    _node = nullptr;
  }
}

void sesync::SyncMaster::setup_node_info() {
  _params[0] = SPA_PARAM_INFO(SPA_PARAM_Props, SPA_PARAM_INFO_READWRITE);
  _info = SPA_NODE_INFO_INIT();
  _info.max_input_ports = 0;
  _info.max_output_ports = 0;
  _info.change_mask =
      SPA_NODE_CHANGE_MASK_FLAGS | SPA_NODE_CHANGE_MASK_PARAMS;
  _info.flags = 0;
  _info.params = _params.data();
  _info.n_params = static_cast<uint32_t>(_params.size());
}

uint64_t sesync::SyncMaster::now_nsec() const {
  timespec ts{};
  clock_gettime(CLOCK_MONOTONIC, &ts);
  return timespec_to_nsec(ts);
}

uint64_t sesync::SyncMaster::beat_period_nsec() const {
  return static_cast<uint64_t>(
      std::llround(60.0 * 1'000'000'000.0 / _config.bpm));
}

uint64_t sesync::SyncMaster::current_beat(const uint64_t now) const {
  const auto period = beat_period_nsec();
  if (now <= _anchor_nsec)
    return _anchor_beat;

  return _anchor_beat + ((now - _anchor_nsec) / period);
}

void sesync::SyncMaster::recompute_schedule() {
  const auto now = now_nsec();
  const auto period = beat_period_nsec();
  const auto current = current_beat(now);

  _schedule.clear();
  for (uint32_t i = 0; i < _config.lookahead_beats; ++i) {
    const auto beat = current + 1 + i;
    const auto nsec = _anchor_nsec + ((beat - _anchor_beat) * period);
    _schedule.push_back(BeatEntry{.beat = beat, .nsec = nsec});
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

  _params_json = std::format(
      R"({{"bpm":[[0,{:.6f}]],"transport_state":[[0,"stopped"]]}})",
      _config.bpm);
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

  return static_cast<spa_pod *>(
      spa_pod_builder_pop(&builder, &object_frame));
}

void sesync::SyncMaster::publish_update() {
  if (_client_node == nullptr)
    return;

  _params[0].flags |= SPA_PARAM_INFO_SERIAL;

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
  (void)flags;
  if (id != SPA_PARAM_Props || param == nullptr)
    return;

  logging::log<logging::LogLevel::Info>(
      "Sync master received SPA_PARAM_Props update");
  recompute_schedule();
  publish_update();
  schedule_next_timer();
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
