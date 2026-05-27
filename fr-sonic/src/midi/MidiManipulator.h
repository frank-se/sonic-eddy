#pragma once

#include <array>
#include <cstdint>
#include <mutex>
#include <string>
#include <vector>

#include <pipewire/loop.h>
#include <pipewire/stream.h>

namespace midi {

struct MidiManipulatorConfig {
  std::string name = "se.midi_manipulator";
  std::string tag;
  std::string description = "Sonic Eddy MIDI manipulator";
};

struct MidiManipulatorRules {
  std::array<bool, 16> drop_channels{};
  std::array<uint8_t, 16> channel_map{};

  MidiManipulatorRules();
};

struct MidiEvent {
  uint32_t offset = 0;
  uint32_t type = 0;
  std::vector<uint8_t> data;
};

class MidiManipulator {
public:
  explicit MidiManipulator(pw_loop *loop, MidiManipulatorConfig config = {});
  ~MidiManipulator();

  MidiManipulator(const MidiManipulator &) = delete;
  MidiManipulator &operator=(const MidiManipulator &) = delete;

  bool start();
  void stop();

  static void capture_process_callback(void *data);
  static void playback_process_callback(void *data);
  static void capture_param_changed_callback(void *data, uint32_t id,
                                             const spa_pod *param);
  static void playback_param_changed_callback(void *data, uint32_t id,
                                              const spa_pod *param);

private:
  pw_loop *_loop;
  MidiManipulatorConfig _config;
  pw_stream *_capture_stream = nullptr;
  pw_stream *_playback_stream = nullptr;
  std::mutex _events_mutex;
  std::vector<MidiEvent> _events;
  MidiManipulatorRules _rules;
  std::array<uint8_t, 4096> _params_buffer{};

  bool setup_capture_stream();
  bool setup_playback_stream();
  void process_capture();
  void process_playback();
  void publish_params();
  void handle_params(uint32_t id, const spa_pod *param);
  void handle_param_value(const char *key, const spa_pod *value);
  void parse_config(const char *config_json);
  [[nodiscard]] bool transform_event(MidiEvent &event) const;
  [[nodiscard]] bool transform_midi_bytes(std::vector<uint8_t> &bytes) const;
  [[nodiscard]] bool transform_ump_bytes(std::vector<uint8_t> &bytes) const;
};

} // namespace midi
