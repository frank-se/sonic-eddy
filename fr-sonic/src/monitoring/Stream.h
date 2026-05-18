#pragma once

#include <atomic>
#include <spa/param/audio/format-utils.h>
#include <pipewire/pipewire.h>

namespace monitoring {

class Stream {
public:
  explicit Stream(uint64_t object_serial, pw_loop *loop)
      : _object_serial(object_serial), _loop(loop) {}

  void process();
  void setup();

  spa_audio_info format{};

  [[nodiscard]] float left_peak()    const { return _left_peak; }
  [[nodiscard]] float left_average() const { return _left_average; }
  [[nodiscard]] float right_peak()   const { return _right_peak; }
  [[nodiscard]] float right_average()const { return _right_average; }
  [[nodiscard]] uint64_t object_serial() const { return _object_serial; }

  void reset() {
    _left_peak    = 0.0f;
    _left_average = 0.0f;
    _right_peak   = 0.0f;
    _right_average= 0.0f;
  }

  void destroy() const { pw_stream_destroy(_stream); }

private:
  uint64_t _object_serial;
  pw_loop *_loop;
  pw_stream *_stream = nullptr;
  std::atomic<float> _left_peak    = 0.0f;
  std::atomic<float> _left_average = 0.0f;
  std::atomic<float> _right_peak   = 0.0f;
  std::atomic<float> _right_average= 0.0f;
};

} // namespace monitoring
