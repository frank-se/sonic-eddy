#include "Monitor.h"

#include <algorithm>

static int setup_stream_function(struct spa_loop *loop, bool async,
                                 uint32_t seq, const void *data, size_t size,
                                 void *user_data) {
  auto stream = static_cast<monitoring::Stream *>(user_data);
  stream->setup();
  return 0;
}

static int destroy_stream_function(struct spa_loop *loop, bool async,
                                   uint32_t seq, const void *data, size_t size,
                                   void *user_data) {
  auto stream = static_cast<monitoring::Stream *>(user_data);
  stream->destroy();
  return 0;
}

void monitoring::Monitor::start_monitor_node(uint64_t object_serial) {
  std::lock_guard lock(_monitoring_streams_mutex);

  const auto exists =
      std::ranges::any_of(_monitoring_streams, [object_serial](auto &s) {
        return s->object_serial() == object_serial;
      });

  if (exists)
    return;

  const auto stream = std::make_shared<Stream>(object_serial, _loop);

  pw_loop_invoke(_loop, setup_stream_function, SPA_ID_INVALID,
                 nullptr, 0, false, stream.get());

  _monitoring_streams.push_back(stream);
}

void monitoring::Monitor::stop_monitor_node(uint64_t object_serial) {
  std::lock_guard lock(_monitoring_streams_mutex);

  const auto it = std::ranges::find_if(
      _monitoring_streams, [object_serial](const auto &s) {
        return s->object_serial() == object_serial;
      });

  if (it == _monitoring_streams.end())
    return;

  auto stream = *it;
  std::erase(_monitoring_streams, stream);
  pw_loop_invoke(_loop, destroy_stream_function, SPA_ID_INVALID,
                 nullptr, 0, true, stream.get());
}

void monitoring::Monitor::forward_measures() {
  std::lock_guard lock(_monitoring_streams_mutex);

  const auto window_ms = static_cast<uint32_t>(_update_interval.count());

  for (auto &&stream : _monitoring_streams) {
    stream->compute_metrics(window_ms);
    _callback(stream->object_serial(),
              stream->left_held_peak(),  stream->right_held_peak(),
              stream->left_rms(),        stream->right_rms());
  }
}

void monitoring::Monitor::start_updates_thread() {
  _running = true;
  _update_thread = std::make_shared<std::thread>([this]() {
    while (_running) {
      forward_measures();
      std::unique_lock lk(_cv_mutex);
      _cv.wait_for(lk, _update_interval, [this] { return !_running.load(); });
    }
  });
}

void monitoring::Monitor::stop_updates_thread() {
  _running = false;
  _cv.notify_one();
  if (_update_thread && _update_thread->joinable())
    _update_thread->join();
}

void monitoring::Monitor::start() { start_updates_thread(); }

void monitoring::Monitor::stop() {
  stop_updates_thread();

  std::lock_guard lock(_monitoring_streams_mutex);
  for (auto &stream : _monitoring_streams) {
    pw_loop_invoke(_loop, destroy_stream_function, SPA_ID_INVALID,
                   nullptr, 0, true, stream.get());
  }
  _monitoring_streams.clear();
}
