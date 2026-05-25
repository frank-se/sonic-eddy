#pragma once

#include <atomic>
#include <cstdint>
#include <optional>
#include <string>
#include <vector>

#include <pipewire/loop.h>
#include <pipewire/stream.h>
#include <spa/param/audio/raw.h>
#include <spa/pod/builder.h>

namespace looper {

struct LooperConfig {
  std::string name = "se.looper";
  std::string tag;
  std::string description = "Sonic Eddy looper";
  std::optional<std::string> capture_target_object;
  std::optional<std::string> playback_target_object;
  uint32_t channels = 2;
  uint32_t max_record_seconds = 300;
  float mix = 0.0f;
  spa_audio_format format = SPA_AUDIO_FORMAT_F32;
};

class Looper {
public:
  explicit Looper(pw_loop *loop, LooperConfig config = {});
  ~Looper();

  Looper(const Looper &) = delete;
  Looper &operator=(const Looper &) = delete;

  bool start();
  void stop();
  void process();

  [[nodiscard]] pw_stream *capture_stream() const { return _capture_stream; }
  [[nodiscard]] pw_stream *playback_stream() const { return _playback_stream; }

  static void capture_process_callback(void *data);
  static void playback_process_callback(void *data);
  static void capture_param_changed_callback(void *data, uint32_t id,
                                             const spa_pod *param);
  static void playback_param_changed_callback(void *data, uint32_t id,
                                              const spa_pod *param);

private:
  pw_loop *_loop;
  LooperConfig _config;
  pw_stream *_capture_stream = nullptr;
  pw_stream *_playback_stream = nullptr;

  spa_audio_info_raw _capture_format{};
  spa_audio_info_raw _playback_format{};
  std::atomic<float> _mix{0.0f};
  std::vector<float> _passthrough_buffer;
  uint32_t _passthrough_frames = 0;
  uint32_t _passthrough_channels = 0;

  bool setup_capture_stream();
  bool setup_playback_stream();
  void capture_passthrough_input(pw_buffer *capture_buffer);
  void write_passthrough_output(pw_buffer *playback_buffer);
  void handle_capture_format(uint32_t id, const spa_pod *param);
  void handle_playback_format(uint32_t id, const spa_pod *param);

  [[nodiscard]] std::string capture_name() const;
  [[nodiscard]] std::string playback_name() const;
  [[nodiscard]] const spa_audio_info_raw &active_format() const;
  [[nodiscard]] pw_stream_flags stream_flags(bool autoconnect) const;

  static const spa_pod *build_audio_format(spa_pod_builder &builder,
                                           spa_audio_format format,
                                           uint32_t channels);
};

} // namespace looper
