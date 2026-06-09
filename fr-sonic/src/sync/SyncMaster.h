#pragma once

#include <array>
#include <cstdint>
#include <optional>
#include <string>
#include <vector>

#include <pipewire/core.h>
#include <pipewire/extensions/client-node.h>
#include <pipewire/loop.h>
#include <pipewire/node.h>
#include <pipewire/timer-queue.h>
#include <spa/node/node.h>
#include <spa/param/param.h>
#include <spa/utils/hook.h>

namespace sesync {

struct SyncMasterConfig {
  uint32_t lookahead_beats = 4;
  uint32_t slide_after_beats = 2;
  uint32_t lead_time_beats = 4;
  double bpm = 120.0;
};

class SyncMaster {
public:
  SyncMaster(pw_core *core, pw_loop *loop, SyncMasterConfig config);
  ~SyncMaster();

  SyncMaster(const SyncMaster &) = delete;
  SyncMaster &operator=(const SyncMaster &) = delete;

  void start();
  void stop();

  [[nodiscard]] uint64_t current_beat_now() const;
  [[nodiscard]] double   current_bpm() const { return _config.bpm; }
  [[nodiscard]] bool     is_playing()   const { return _transport_state != "stopped"; }

  static int set_param(void *object, uint32_t id, uint32_t flags,
                       const spa_pod *param);

private:
  struct BeatEntry {
    uint64_t beat;
    uint64_t nsec;
  };

  pw_core *_core;
  pw_loop *_loop;
  SyncMasterConfig _config;

  pw_client_node *_client_node = nullptr;
  pw_node *_node = nullptr;
  spa_hook _client_node_listener{};
  spa_node_info _info{};
  std::array<spa_param_info, 1> _params{};
  uint32_t _param_serial = 0;

  pw_timer_queue *_timer_queue = nullptr;
  pw_timer _timer{};

  uint64_t _anchor_beat = 0;
  uint64_t _anchor_nsec = 0;
  uint64_t _bpm_beat = 0;
  uint64_t _transport_state_beat = 0;
  std::string _transport_state = "stopped";
  std::optional<uint64_t> _pending_bpm_beat;
  std::optional<double> _pending_bpm;
  std::optional<uint64_t> _pending_transport_state_beat;
  std::optional<std::string> _pending_transport_state;
  std::vector<BeatEntry> _schedule{};
  std::string _schedule_json;
  std::string _params_json;

  std::array<uint8_t, 8192> _param_buffer{};

  void setup_node_info();
  void recompute_schedule();
  void rebuild_json();
  void publish_update();
  void schedule_next_timer();
  void on_timer();
  void handle_set_param(uint32_t id, uint32_t flags, const spa_pod *param);
  bool handle_params_value(const char *key, const char *value);
  bool apply_beat_params_patch(const std::string &json);
  bool apply_bpm_change(uint64_t beat, double bpm);
  bool apply_transport_state_change(uint64_t beat, std::string state);
  void activate_due_changes(uint64_t now);

  [[nodiscard]] uint64_t now_nsec() const;
  [[nodiscard]] uint64_t beat_period_nsec() const;
  [[nodiscard]] uint64_t beat_period_nsec(double bpm) const;
  [[nodiscard]] uint64_t current_beat(uint64_t now) const;
  [[nodiscard]] uint64_t nsec_for_beat(uint64_t beat) const;
  [[nodiscard]] bool accepts_change(uint64_t beat, uint64_t current) const;
  [[nodiscard]] spa_pod *build_props_pod();

  static void timer_callback(void *data);

};

} // namespace sesync
