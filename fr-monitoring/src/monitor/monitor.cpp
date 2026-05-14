#include "monitor/monitor.h"

#include <algorithm>

static int setup_stream_function(struct spa_loop *loop, bool async,
                                 uint32_t seq, const void *data, size_t size,
                                 void *user_data) {
  auto stream = static_cast<streams::Stream *>(user_data);
  stream->setup();
  return 0;
}

void monitor::Monitor::start_monitor_node(uint64_t object_serial) {
  std::lock_guard lock(_monitoring_streams_mutex);

  const auto exists =
      std::ranges::any_of(_monitoring_streams, [object_serial](auto &stream) {
        return stream->object_serial() == object_serial;
      });

  if (exists)
    return;

  const auto stream = std::make_shared<streams::Stream>(object_serial, _loop);

  pw_loop_invoke(pw_main_loop_get_loop(_loop), setup_stream_function,
                 SPA_ID_INVALID, nullptr, 0, false, stream.get());

  _monitoring_streams.push_back(stream);
}

void monitor::Monitor::stop_monitor_node(uint64_t object_serial) {
  std::lock_guard lock(_monitoring_streams_mutex);

  const auto stream = std::ranges::find_if(
      _monitoring_streams, [object_serial](const auto &stream) {
        return stream->object_serial() == object_serial;
      });

  std::erase(_monitoring_streams, *stream);

  (*stream)->destroy();
}

void monitor::Monitor::forward_measures() {
  std::lock_guard lock(_monitoring_streams_mutex);

  for (auto &&stream : _monitoring_streams) {
    _callback(stream->object_serial(), stream->left_peak(),
              stream->right_peak(), stream->left_average(),
              stream->right_average());
  }
}

void monitor::Monitor::start_updates_thread() {
  _update_thread = std::make_shared<std::thread>([this]() {
    while (true) {
      forward_measures();
      std::this_thread::sleep_for(_update_interval);
    }
  });
}

void monitor::Monitor::stop_updates_thread() const {
  pthread_kill(_update_thread->native_handle(), SIGINT);
  _update_thread->join();
}

void monitor::Monitor::stop_pipewire_thread() const {
  pthread_kill(_pipewire_thread->native_handle(), SIGINT);
  _pipewire_thread->join();
}

static void do_quit(void *user_data, int signal_number) {
  const auto monitor = static_cast<monitor::Monitor *>(user_data);
  monitor->quit_main_loop();
}

void monitor::Monitor::setup_pipewire() {
  pw_init(nullptr, nullptr);
  _loop = pw_main_loop_new(nullptr);

  pw_loop_add_signal(pw_main_loop_get_loop(_loop), SIGINT, do_quit, this);
  pw_loop_add_signal(pw_main_loop_get_loop(_loop), SIGTERM, do_quit, this);
}

void monitor::Monitor::quit_main_loop() const { pw_main_loop_quit(_loop); }

void monitor::Monitor::start() {
  start_updates_thread();
  start_pipewire_thread();
}

void monitor::Monitor::stop() {
  stop_pipewire_thread();
  stop_updates_thread();
}

void monitor::Monitor::start_pipewire_thread() {
  std::binary_semaphore semaphore{0};
  _pipewire_thread = std::make_shared<std::thread>([this, &semaphore]() {
    setup_pipewire();
    semaphore.release();
    pw_main_loop_run(_loop);
  });
  semaphore.acquire();
}
