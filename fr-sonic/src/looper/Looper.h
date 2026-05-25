#pragma once

#include <atomic>
#include <array>
#include <cstdint>
#include <memory>
#include <optional>
#include <string>
#include <vector>

#include <boost/lockfree/spsc_queue.hpp>
#include <pipewire/loop.h>
#include <pipewire/stream.h>
#include <spa/param/audio/raw.h>
#include <spa/pod/builder.h>

namespace sesync {
class SyncClient;
}

namespace looper {

enum class CommandKind : uint32_t {
  CutRange = 1,
  CutLength = 2,
  Play = 3,
  Stop = 4,
  Archive = 5,
};

struct CommandEvent {
  CommandKind kind = CommandKind::Stop;
  uint64_t scheduled_beat = 0;
  uint64_t start_beat = 0;
  uint64_t end_beat = 0;
  uint64_t loop_length = 0;
  uint32_t loop_number = 0;
};

struct LoopSlot {
  std::vector<float> samples;
  uint64_t generation = 0;
  uint64_t length_frames = 0;
  uint64_t playhead_frame = 0;
  uint32_t channels = 0;
  bool ready = false;
  bool playing = false;
};

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
  explicit Looper(pw_loop *loop, LooperConfig config = {},
                  std::shared_ptr<sesync::SyncClient> sync_client = {});
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
  std::shared_ptr<sesync::SyncClient> _sync_client;

  spa_audio_info_raw _capture_format{};
  spa_audio_info_raw _playback_format{};
  std::atomic<float> _mix{0.0f};
  boost::lockfree::spsc_queue<CommandEvent, boost::lockfree::capacity<256>>
      _command_events;
  std::array<CommandEvent, 256> _pending_commands{};
  size_t _pending_command_count = 0;
  std::array<uint8_t, 4096> _params_buffer{};
  uint64_t _processed_command_count = 0;
  uint64_t _dropped_command_count = 0;
  uint64_t _record_write_frame = 0;
  uint64_t _recorded_frames = 0;
  uint64_t _record_capacity_frames = 0;
  std::vector<float> _record_buffer;
  std::array<LoopSlot, 10> _loop_slots;
  std::vector<float> _passthrough_buffer;
  uint32_t _passthrough_frames = 0;
  uint32_t _passthrough_channels = 0;

  bool setup_capture_stream();
  bool setup_playback_stream();
  void capture_passthrough_input(pw_buffer *capture_buffer);
  void write_passthrough_output(pw_buffer *playback_buffer);
  void drain_command_events();
  void queue_pending_command(const CommandEvent &event);
  void process_pending_commands();
  void apply_command_event(const CommandEvent &event);
  void cut_length(uint64_t length_seconds, uint32_t loop_number);
  void play_loop(uint32_t loop_number);
  void stop_loops();
  float render_wet_sample(uint32_t channel);
  void publish_params();
  void handle_capture_format(uint32_t id, const spa_pod *param);
  void handle_playback_format(uint32_t id, const spa_pod *param);
  void handle_params(uint32_t id, const spa_pod *param);
  void handle_param_value(const char *key, const spa_pod *value);
  void enqueue_command(const CommandEvent &event);
  void parse_commands_param(const char *value);

  [[nodiscard]] std::string capture_name() const;
  [[nodiscard]] std::string playback_name() const;
  [[nodiscard]] const spa_audio_info_raw &active_format() const;
  [[nodiscard]] pw_stream_flags stream_flags(bool autoconnect) const;
  [[nodiscard]] std::optional<uint64_t> current_sync_beat() const;

  static const spa_pod *build_audio_format(spa_pod_builder &builder,
                                           spa_audio_format format,
                                           uint32_t channels);
};

} // namespace looper
