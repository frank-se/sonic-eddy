#pragma once

#include <boost/lockfree/spsc_queue.hpp>
#include <chrono>
#include <cstdint>
#include <spa/param/audio/format-utils.h>
#include <pipewire/pipewire.h>

namespace monitoring {

struct BufferEntry {
    std::chrono::steady_clock::time_point timestamp;
    float peak[2];
    float sum_sq[2];
    uint32_t samples;
};

class Stream {
public:
  explicit Stream(uint64_t object_serial, pw_loop *loop)
      : _object_serial(object_serial), _loop(loop) {}

  void process();
  void setup();

  spa_audio_info format{};

  // Called from the update thread; window_ms is the sliding window duration.
  void compute_metrics(uint32_t window_ms);

  [[nodiscard]] float left_rms()        const { return _left_rms; }
  [[nodiscard]] float right_rms()       const { return _right_rms; }
  [[nodiscard]] float left_held_peak()  const { return _left_held_peak; }
  [[nodiscard]] float right_held_peak() const { return _right_held_peak; }
  [[nodiscard]] uint64_t object_serial()const { return _object_serial; }

  void destroy() const { pw_stream_destroy(_stream); }
  [[nodiscard]] pw_stream *get_stream() const { return _stream; }

private:
  uint64_t _object_serial;
  pw_loop *_loop;
  pw_stream *_stream = nullptr;

  // SPSC queue — written by RT process(), drained by update thread.
  boost::lockfree::spsc_queue<BufferEntry,
      boost::lockfree::capacity<512>> _queue;

  // Sliding window: entries drained from _queue are kept here until expired.
  std::vector<BufferEntry> _window;

  // Metrics maintained by the update thread.
  float _left_rms        = 0.0f;
  float _right_rms       = 0.0f;
  float _left_held_peak  = 0.0f;
  float _right_held_peak = 0.0f;

  std::chrono::steady_clock::time_point _left_held_peak_time{};
  std::chrono::steady_clock::time_point _right_held_peak_time{};

  static constexpr uint32_t HOLD_DURATION_MS = 1500;
};

} // namespace monitoring
