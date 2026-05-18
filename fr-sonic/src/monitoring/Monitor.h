#pragma once

#include "Stream.h"

#include <chrono>
#include <functional>
#include <memory>
#include <mutex>
#include <thread>
#include <vector>

#include <pipewire/pipewire.h>

namespace monitoring {

class Monitor {
public:
  Monitor(pw_loop *loop,
          std::function<void(uint64_t, float, float, float, float)> callback,
          uint64_t update_interval_milliseconds)
      : _loop(loop), _callback(std::move(callback)),
        _update_interval(std::chrono::milliseconds(update_interval_milliseconds)) {}

  void start();
  void stop();

  void start_monitor_node(uint64_t object_serial);
  void stop_monitor_node(uint64_t object_serial);

private:
  pw_loop *_loop;
  std::function<void(uint64_t, float, float, float, float)> _callback;
  std::chrono::milliseconds _update_interval{};

  std::vector<std::shared_ptr<Stream>> _monitoring_streams;
  std::mutex _monitoring_streams_mutex;

  std::shared_ptr<std::thread> _update_thread;

  void forward_measures();
  void start_updates_thread();
  void stop_updates_thread() const;
};

} // namespace monitoring
