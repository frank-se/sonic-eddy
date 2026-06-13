#pragma once

#include <atomic>
#include <cstdint>
#include <string>

#include <boost/lockfree/spsc_queue.hpp>
#include <pipewire/loop.h>
#include <pipewire/stream.h>
#include <spa/param/audio/raw.h>

namespace ducker {

struct DuckerConfig {
  std::string name = "se.ducker";
  std::string tag;
  std::string description = "Sonic Eddy ducker";
  std::string capture_target_object;
  std::string playback_target_object;
};

struct MidiNoteEvent {
  bool note_on;
  uint8_t note;
  uint8_t velocity;
};

enum class DuckerPhase { Idle, Attack, Release };

class Ducker {
public:
  explicit Ducker(pw_loop *loop, DuckerConfig config = {});
  ~Ducker();

  Ducker(const Ducker &) = delete;
  Ducker &operator=(const Ducker &) = delete;

  bool start();
  void stop();

  static void midi_capture_process_callback(void *data);
  static void audio_capture_process_callback(void *data);
  static void audio_playback_process_callback(void *data);
  static void audio_capture_param_changed_callback(void *data, uint32_t id,
                                                   const spa_pod *param);
  static void audio_playback_param_changed_callback(void *data, uint32_t id,
                                                    const spa_pod *param);

private:
  pw_loop *_loop;
  DuckerConfig _config;

  pw_stream *_midi_capture_stream = nullptr;
  pw_stream *_audio_capture_stream = nullptr;
  pw_stream *_audio_playback_stream = nullptr;

  /* MIDI → audio thread communication */
  boost::lockfree::spsc_queue<MidiNoteEvent, boost::lockfree::capacity<64>>
      _midi_events;

  /* Envelope state — only touched in audio RT thread */
  DuckerPhase _phase = DuckerPhase::Idle;
  float _current_gain = 1.0f;
  float _start_gain = 1.0f;
  float _target_gain = 1.0f;
  float _phase_t = 0.0f;      /* normalized 0→1 progress through current phase */
  float _phase_step = 0.0f;   /* advance per sample */
  float _applied_depth = 0.0f;

  /* Params — written from non-RT, read in RT */
  std::atomic<float> _param_attack{0.05f};
  std::atomic<float> _param_release{0.15f};
  std::atomic<float> _param_depth{1.0f};
  std::atomic<int> _param_attack_shape{0};
  std::atomic<int> _param_release_shape{0};
  std::atomic<bool> _param_bypass{false};
  std::atomic<int> _param_midi_channel{0}; /* 0-based, 0 = channel 1 */

  spa_audio_info_raw _audio_format{};
  std::array<uint8_t, 4096> _params_buffer{};

  bool setup_midi_capture_stream();
  bool setup_audio_capture_stream();
  bool setup_audio_playback_stream();

  void process_midi_capture();
  void process_audio_capture();
  void process_audio_playback();

  void handle_audio_format(uint32_t id, const spa_pod *param);
  void handle_audio_params(uint32_t id, const spa_pod *param);
  void handle_param_value(const char *key, const spa_pod *value);
  void publish_params();

  void on_note_on(uint8_t note, uint8_t velocity);
  void on_note_off();
  float advance_envelope(uint32_t n_samples);
  static float apply_curve(float t, int shape);
};

} // namespace ducker
