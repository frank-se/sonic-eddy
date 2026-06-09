#include "LinkSyncAdapter.h"

#include "logging/log.h"

#include <ctime>

namespace {

uint64_t timespec_now_nsec() {
  timespec ts{};
  clock_gettime(CLOCK_MONOTONIC, &ts);
  return static_cast<uint64_t>(ts.tv_sec) * 1'000'000'000ull +
         static_cast<uint64_t>(ts.tv_nsec);
}

} // namespace

sesync::LinkSyncAdapter::LinkSyncAdapter(pw_loop *loop, SyncMaster *master)
    : _loop(loop), _master(master), _link(master->current_bpm()) {

  _link.enableStartStopSync(true);
  _timer_queue = pw_timer_queue_new(_loop);
  recorrelate_clocks();
}

sesync::LinkSyncAdapter::~LinkSyncAdapter() {
  _link.enable(false);
  if (_timer_queue != nullptr) {
    pw_timer_queue_cancel(&_timer);
    pw_timer_queue_destroy(_timer_queue);
  }
}

void sesync::LinkSyncAdapter::enable(bool enable) {
  _link.enable(enable);
  if (enable) {
    recorrelate_clocks();
    push_state();
    schedule_timer();
    logging::log<logging::LogLevel::Info>(
        "Ableton Link enabled — peers={} bpm={:.2f} playing={} clock_offset_ns={}",
        _link.numPeers(), _master->current_bpm(), _master->is_playing(),
        _clock_offset_ns);
  } else {
    if (_timer_queue)
      pw_timer_queue_cancel(&_timer);
    logging::log<logging::LogLevel::Info>("Ableton Link disabled");
  }
}

void sesync::LinkSyncAdapter::set_peers_callback(
    std::function<void(std::size_t)> cb) {
  _peers_callback = std::move(cb);
  _link.setNumPeersCallback([this](const std::size_t peers) {
    logging::log<logging::LogLevel::Info>(
        "Ableton Link peers changed: {}", peers);
    if (_peers_callback)
      _peers_callback(peers);
  });
}

void sesync::LinkSyncAdapter::schedule_timer() {
  if (_timer_queue == nullptr)
    return;
  pw_timer_queue_cancel(&_timer);
  pw_timer_queue_add(_timer_queue, &_timer, nullptr, TIMER_INTERVAL_NS,
                     timer_callback, this);
}

void sesync::LinkSyncAdapter::recorrelate_clocks() {
  const auto link_us = _link.clock().micros().count();
  const auto mono_ns = static_cast<int64_t>(timespec_now_nsec());
  _clock_offset_ns   = mono_ns - link_us * 1000;
  _next_recorrelate  = mono_ns + RECORRELATE_INTERVAL_NS;
}

uint64_t sesync::LinkSyncAdapter::now_nsec() const {
  return timespec_now_nsec();
}

std::chrono::microseconds
sesync::LinkSyncAdapter::mono_to_link(const uint64_t mono_ns) const {
  return std::chrono::microseconds{
      (static_cast<int64_t>(mono_ns) - _clock_offset_ns) / 1000};
}

void sesync::LinkSyncAdapter::push_state() {
  const auto mono_ns  = now_nsec();
  const auto link_now = mono_to_link(mono_ns);

  const auto beat    = static_cast<double>(_master->current_beat_now());
  const auto bpm     = _master->current_bpm();
  const auto playing = _master->is_playing();

  auto state = _link.captureAppSessionState();
  state.setTempo(bpm, link_now);
  state.requestBeatAtTime(beat, link_now, 1.0);
  state.setIsPlaying(playing, link_now);
  _link.commitAppSessionState(state);
}

void sesync::LinkSyncAdapter::on_timer() {
  const auto mono_ns = now_nsec();
  if (static_cast<int64_t>(mono_ns) >= _next_recorrelate)
    recorrelate_clocks();

  push_state();
  schedule_timer();
}

void sesync::LinkSyncAdapter::timer_callback(void *data) {
  static_cast<LinkSyncAdapter *>(data)->on_timer();
}
