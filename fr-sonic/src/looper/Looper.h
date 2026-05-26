#pragma once

#include <atomic>
#include <array>
#include <condition_variable>
#include <cstdint>
#include <memory>
#include <mutex>
#include <optional>
#include <string>
#include <thread>
#include <vector>

#include <boost/lockfree/spsc_queue.hpp>
#include <pipewire/loop.h>
#include <pipewire/stream.h>
#include <spa/param/audio/raw.h>
#include <spa/pod/builder.h>

namespace sesync {
class SyncClient;
struct SyncSnapshot;
struct TransportStateEntry;
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
  std::shared_ptr<std::vector<float>> samples;
  uint64_t generation = 0;
  std::optional<uint64_t> start_beat;
  std::optional<uint64_t> end_beat;
  std::optional<uint64_t> length_beats;
  std::optional<uint64_t> play_started_beat;
  uint64_t length_frames = 0;
  uint64_t playhead_frame = 0;
  uint64_t ring_start_frame = 0;
  uint32_t sample_rate = 0;
  uint32_t channels = 0;
  std::optional<double> bpm;
  bool ready = false;
  bool playing = false;
  bool owned = false;
};

struct LoopCopyJob {
  uint32_t loop_number = 0;
  uint64_t generation = 0;
  uint64_t ring_start_frame = 0;
  uint64_t length_frames = 0;
  uint32_t channels = 0;
};

struct LoopCopyResult {
  uint32_t loop_number = 0;
  uint64_t generation = 0;
  uint64_t length_frames = 0;
  uint32_t channels = 0;
  uint64_t elapsed_usec = 0;
  std::shared_ptr<std::vector<float>> samples;
};

struct LoopStateEntry {
  uint32_t loop_number = 0;
  uint64_t generation = 0;
  uint64_t start_beat = 0;
  uint64_t end_beat = 0;
  uint64_t length_beats = 0;
  uint64_t length_frames = 0;
  uint64_t playhead_frame = 0;
  uint64_t play_started_beat = 0;
  uint32_t sample_rate = 0;
  uint32_t channels = 0;
  double bpm = 0.0;
  bool populated = false;
  bool playing = false;
  bool owned = false;
  bool has_start_beat = false;
  bool has_end_beat = false;
  bool has_length_beats = false;
  bool has_play_started_beat = false;
  bool has_bpm = false;
};

struct LooperStateUpdate {
  std::array<LoopStateEntry, 10> loops{};
  uint64_t transport_start_beat = 0;
  uint64_t ring_buffer_zero_beat = 0;
  bool recording = false;
  bool has_transport_start_beat = false;
  bool has_ring_buffer_zero_beat = false;
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
  boost::lockfree::spsc_queue<LoopCopyJob, boost::lockfree::capacity<32>>
      _copy_jobs;
  boost::lockfree::spsc_queue<LoopCopyResult, boost::lockfree::capacity<32>>
      _copy_results;
  boost::lockfree::spsc_queue<std::shared_ptr<std::vector<float>>,
                              boost::lockfree::capacity<32>>
      _retired_sample_buffers;
  std::array<CommandEvent, 256> _pending_commands{};
  size_t _pending_command_count = 0;
  boost::lockfree::spsc_queue<LooperStateUpdate, boost::lockfree::capacity<32>>
      _state_updates;
  std::array<uint8_t, 16384> _params_buffer{};
  std::string _published_state_json =
      R"({"version":1,"active_loop":null,"loops":[],"recording":false,"transport_alignment":{"transport_start_beat":null,"ring_buffer_zero_beat":null},"active_playback":null,"pending_jobs":[],"last_command_failure":null})";
  uint64_t _processed_command_count = 0;
  uint64_t _dropped_command_count = 0;
  uint64_t _record_write_frame = 0;
  uint64_t _recorded_frames = 0;
  bool _recording = false;
  std::optional<uint64_t> _transport_start_beat;
  std::optional<uint64_t> _ring_buffer_zero_beat;
  uint64_t _last_capture_frames = 0;
  std::optional<uint64_t> _command_record_write_frame;
  std::optional<CommandEvent> _active_command_event;
  uint64_t _record_capacity_frames = 0;
  std::vector<float> _record_buffer;
  std::array<LoopSlot, 10> _loop_slots;
  std::vector<float> _passthrough_buffer;
  uint32_t _passthrough_frames = 0;
  uint32_t _passthrough_channels = 0;
  std::thread _copy_thread;
  std::atomic<bool> _copy_thread_running{false};
  std::mutex _copy_wakeup_mutex;
  std::condition_variable _copy_wakeup;

  bool setup_capture_stream();
  bool setup_playback_stream();
  void start_copy_thread();
  void stop_copy_thread();
  void copy_thread_main();
  void drain_retired_sample_buffers();
  void drain_state_updates();
  void publish_state_update(std::string state_json);
  void capture_passthrough_input(pw_buffer *capture_buffer);
  bool should_record_frame(const sesync::SyncSnapshot &snapshot,
                           uint64_t frame_nsec, uint32_t rate);
  void stop_recording();
  void write_passthrough_output(pw_buffer *playback_buffer);
  void drain_command_events();
  void drain_copy_results();
  void queue_pending_command(const CommandEvent &event);
  void process_due_commands(uint32_t frame_offset, uint64_t buffer_start_nsec,
                            uint32_t rate);
  void apply_command_event(const CommandEvent &event);
  void cut_length(uint64_t length_seconds, uint32_t loop_number);
  void enqueue_copy_job(const LoopSlot &slot, uint32_t loop_number);
  void retire_samples(std::shared_ptr<std::vector<float>> samples);
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
  void enqueue_state_update();

  [[nodiscard]] std::string capture_name() const;
  [[nodiscard]] std::string playback_name() const;
  [[nodiscard]] const spa_audio_info_raw &active_format() const;
  [[nodiscard]] pw_stream_flags stream_flags(bool autoconnect) const;
  [[nodiscard]] std::optional<uint64_t> current_sync_beat() const;
  [[nodiscard]] std::optional<double> bpm_at_beat(uint64_t beat) const;
  [[nodiscard]] std::optional<uint64_t>
  scheduled_beat_nsec(const sesync::SyncSnapshot &snapshot,
                      uint64_t beat) const;
  [[nodiscard]] std::optional<sesync::TransportStateEntry>
  transport_state_entry_at(const sesync::SyncSnapshot &snapshot,
                           uint64_t beat) const;
  [[nodiscard]] std::optional<int64_t>
  command_frame_offset(const CommandEvent &event, uint64_t buffer_start_nsec,
                       uint32_t rate) const;
  [[nodiscard]] LooperStateUpdate capture_state_update() const;
  [[nodiscard]] std::string
  format_state_json(const LooperStateUpdate &update) const;

  static const spa_pod *build_audio_format(spa_pod_builder &builder,
                                           spa_audio_format format,
                                           uint32_t channels);
};

} // namespace looper
