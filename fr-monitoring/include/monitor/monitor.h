#pragma once

#include "streams/stream.h"

#include <chrono>
#include <functional>
#include <memory>
#include <thread>

namespace monitor {

class Monitor {
public:
  Monitor(
      const std::function<void(uint64_t, float, float, float, float)> &callback,
      const uint64_t update_interval_milliseconds) {
    _callback = callback;
    _update_interval = std::chrono::milliseconds(update_interval_milliseconds);
  }

  void setup_pipewire();
  void quit_main_loop() const;

  void start_monitor_node(uint64_t object_serial);
  void stop_monitor_node(uint64_t object_serial);

  void start();
  void stop();

private:
  std::function<void(uint64_t, float, float, float, float)> _callback;

  pw_main_loop *_loop = nullptr;

  std::vector<std::shared_ptr<streams::Stream>> _monitoring_streams;
  std::mutex _monitoring_streams_mutex;
  std::chrono::milliseconds _update_interval{};

  std::shared_ptr<std::thread> _update_thread = nullptr;
  std::shared_ptr<std::thread> _pipewire_thread = nullptr;

  void forward_measures();
  void start_updates_thread();
  void start_pipewire_thread();
  void stop_updates_thread() const;
  void stop_pipewire_thread() const;
};

} // namespace monitor
