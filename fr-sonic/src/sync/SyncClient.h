#pragma once

#include <atomic>
#include <cstdint>
#include <functional>
#include <memory>
#include <optional>
#include <string>
#include <vector>

#include <pipewire/core.h>
#include <pipewire/loop.h>
#include <pipewire/node.h>
#include <pipewire/proxy.h>
#include <spa/param/param.h>
#include <spa/utils/hook.h>

namespace sesync {

enum class TransportState { Stopped, StartScheduled, Playing };

struct BeatScheduleEntry {
  uint64_t beat;
  uint64_t nsec;
};

struct BpmEntry {
  uint64_t beat;
  double bpm;
};

struct TransportStateEntry {
  uint64_t beat;
  TransportState state;
};

struct SyncSnapshot {
  uint32_t master_object_id = 0;
  std::vector<BeatScheduleEntry> beat_history;
  std::vector<BeatScheduleEntry> beat_schedule;
  std::vector<BpmEntry> bpm;
  std::vector<TransportStateEntry> transport_states;

  [[nodiscard]] std::optional<BeatScheduleEntry>
  current_beat(uint64_t now_nsec) const;
  [[nodiscard]] TransportState transport_state_at(uint64_t beat) const;
  [[nodiscard]] double bpm_at(uint64_t beat) const;
};

class SyncClient {
public:
  using SnapshotPtr = std::shared_ptr<const SyncSnapshot>;
  using SnapshotCallback = std::function<void(SnapshotPtr)>;

  SyncClient(pw_core *core, pw_loop *loop);
  ~SyncClient();

  SyncClient(const SyncClient &) = delete;
  SyncClient &operator=(const SyncClient &) = delete;

  void start();
  void stop();

  [[nodiscard]] SnapshotPtr snapshot() const;
  [[nodiscard]] std::optional<BeatScheduleEntry>
  current_beat(uint64_t now_nsec) const;

  void set_snapshot_callback(SnapshotCallback callback);

  static void registry_global(void *data, uint32_t id, uint32_t permissions,
                              const char *type, uint32_t version,
                              const spa_dict *props);
  static void registry_global_remove(void *data, uint32_t id);
  static void node_param(void *data, int seq, uint32_t id, uint32_t index,
                         uint32_t next, const spa_pod *param);

private:
  pw_core *_core;
  pw_loop *_loop;
  pw_registry *_registry = nullptr;
  pw_node *_master_node = nullptr;
  uint32_t _master_object_id = 0;
  uint64_t _master_object_serial = 0;

  spa_hook _registry_listener{};
  spa_hook _node_listener{};

  std::atomic<SnapshotPtr> _snapshot;
  SnapshotCallback _snapshot_callback;

  void handle_global(uint32_t id, const char *type, uint32_t version,
                     const spa_dict *props);
  void handle_global_remove(uint32_t id);
  void bind_master(uint32_t id, uint32_t version, uint64_t serial);
  void destroy_master();
  void subscribe_master_params();
  void handle_param(uint32_t id, const spa_pod *pod);
  void apply_params(const std::string &schedule_json,
                    const std::string &params_json);

};

} // namespace sesync
