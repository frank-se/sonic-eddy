#pragma once

#include "sync/SyncClient.h"

#include <cstdint>
#include <optional>
#include <pipewire/loop.h>
#include <pipewire/stream.h>
#include <spa/pod/builder.h>
#include <spa/utils/hook.h>

namespace midi {

class MidiSyncSender {
public:
  MidiSyncSender(pw_loop *loop, sesync::SyncClient &sync_client);
  ~MidiSyncSender();

  MidiSyncSender(const MidiSyncSender &) = delete;
  MidiSyncSender &operator=(const MidiSyncSender &) = delete;

  void start();
  void stop();
  void process();

  static void process_callback(void *data);

private:
  static constexpr uint64_t PulsesPerBeat = 24;

  pw_loop *_loop;
  sesync::SyncClient &_sync_client;
  pw_stream *_stream = nullptr;

  spa_hook _stream_listener{};
  sesync::TransportState _last_transport_state =
      sesync::TransportState::Stopped;
  std::optional<uint64_t> _last_emitted_tick;

  void setup_stream();
  void emit_due_messages(spa_pod_builder &builder, uint64_t now_nsec);
  void emit_byte(spa_pod_builder &builder, uint8_t byte) const;

  [[nodiscard]] uint64_t now_nsec() const;
  [[nodiscard]] std::optional<uint64_t>
  tick_nsec(const sesync::SyncSnapshot &snapshot, uint64_t tick) const;

};

} // namespace midi
