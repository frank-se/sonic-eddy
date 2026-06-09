#pragma once

#include "SyncMaster.h"

#include <ableton/Link.hpp>
#include <chrono>
#include <cstddef>
#include <cstdint>
#include <functional>
#include <pipewire/loop.h>
#include <pipewire/timer-queue.h>

namespace sesync {

class LinkSyncAdapter {
public:
  LinkSyncAdapter(pw_loop *loop, SyncMaster *master);
  ~LinkSyncAdapter();

  LinkSyncAdapter(const LinkSyncAdapter &) = delete;
  LinkSyncAdapter &operator=(const LinkSyncAdapter &) = delete;

  void enable(bool enable);
  [[nodiscard]] bool        is_enabled()  const { return _link.isEnabled(); }
  [[nodiscard]] std::size_t num_peers()   const { return _link.numPeers(); }

  void set_quantum(double quantum)        { _quantum = quantum; }
  void set_peers_callback(std::function<void(std::size_t)> cb);

private:
  void schedule_timer();
  void on_timer();
  void push_state();
  void recorrelate_clocks();

  [[nodiscard]] uint64_t now_nsec() const;
  [[nodiscard]] std::chrono::microseconds mono_to_link(uint64_t mono_ns) const;

  pw_loop        *_loop;
  SyncMaster     *_master;
  pw_timer_queue *_timer_queue = nullptr;
  pw_timer        _timer{};

  ableton::Link _link;
  double   _quantum          = 4.0;
  int64_t  _clock_offset_ns  = 0;   // mono_ns - link_us*1000
  int64_t  _next_recorrelate = 0;   // mono_ns of next recorrelation

  std::function<void(std::size_t)> _peers_callback;

  static constexpr int64_t RECORRELATE_INTERVAL_NS = 10'000'000'000LL; // 10s
  static constexpr int64_t TIMER_INTERVAL_NS        =    250'000'000LL; // 250ms

  static void timer_callback(void *data);
};

} // namespace sesync
