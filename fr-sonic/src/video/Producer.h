#pragma once

#include <atomic>
#include <cstddef>
#include <cstdint>
#include <mutex>
#include <string>
#include <vector>

#include <pipewire/loop.h>
#include <pipewire/stream.h>
#include <spa/pod/pod.h>

namespace video {

struct ProducerConfig {
  std::string name;
  std::string description;
  uint32_t width = 0;
  uint32_t height = 0;
};

// A fixed-format (RGBA) PipeWire video source node fed by update_frame()
// rather than by anything PipeWire produces upstream - the source of frames
// is arbitrary (e.g. a rendered UI bitmap). update_frame() is real-time-safe
// to call from any thread: it only ever touches the scratch buffer under a
// mutex, mirroring the InputSlot hand-off pattern in
// pw-video-compositor/src/main.cpp. The PipeWire process() callback (RT
// thread) only ever reads that buffer - no caller of update_frame() ever
// touches PipeWire itself.
class Producer {
public:
  // Must be constructed and destroyed on the owning pw_loop's thread (same
  // requirement as every other owned-node subsystem in this library, e.g.
  // looper::Looper - see invoke_owned_pipewire_sync in frsonic.cpp).
  Producer(pw_loop *loop, ProducerConfig config);
  ~Producer();

  Producer(const Producer &) = delete;
  Producer &operator=(const Producer &) = delete;

  // Connects the pw_stream. Must be called on the owning pw_loop's thread.
  bool start();

  // Destroys the pw_stream. Must be called on the owning pw_loop's thread.
  void stop();

  // Copies `size` bytes of tightly-packed RGBA pixel data (must equal
  // width*height*4) into the scratch buffer under lock. Safe to call from
  // any thread, does not touch PipeWire.
  bool update_frame(const uint8_t *rgba, size_t size);

private:
  static void on_process(void *data);
  static void on_param_changed(void *data, uint32_t id, const spa_pod *param);
  static const pw_stream_events kStreamEvents;

  pw_loop *_loop;
  ProducerConfig _config;
  pw_stream *_stream = nullptr;
  // Set before pw_stream_destroy() so any callback pw_stream_destroy()
  // itself synchronously triggers (e.g. a final drain/process call while
  // the stream is already mid-teardown) becomes a no-op instead of
  // touching a stream/buffer that's in the process of being freed.
  std::atomic_bool _stopping{false};

  std::vector<uint8_t> _frame;
  std::mutex _frame_mutex;
  bool _has_frame = false;
};

} // namespace video
