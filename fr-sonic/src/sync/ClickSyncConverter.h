#pragma once

#include "SyncClient.h"

#include <cstdint>
#include <memory>
#include <pipewire/loop.h>
#include <pipewire/stream.h>
#include <string>

namespace sesync {

struct ClickSyncConfig {
  std::string id;
  std::string name;
  std::string tag;
  uint32_t pulses_per_quarter_note = 24;
  double pulse_length_ms = 5.0;
  float pulse_amplitude = 0.75f;
};

class ClickSyncConverter {
public:
  ClickSyncConverter(pw_loop *loop, ClickSyncConfig config,
                     std::shared_ptr<SyncClient> sync_client);
  ~ClickSyncConverter();

  ClickSyncConverter(const ClickSyncConverter &) = delete;
  ClickSyncConverter &operator=(const ClickSyncConverter &) = delete;

  bool start();
  void stop();
  static void process_callback(void *data);

private:
  enum class OutputKind { Click, Reset, Run };

  struct Output {
    ClickSyncConverter *converter = nullptr;
    OutputKind kind = OutputKind::Click;
    pw_stream *stream = nullptr;
    uint32_t last_cycle_frames = 1024;
  };

  pw_loop *_loop;
  ClickSyncConfig _config;
  std::shared_ptr<SyncClient> _sync_client;
  Output _click_output{this, OutputKind::Click};
  Output _reset_output{this, OutputKind::Reset};
  Output _run_output{this, OutputKind::Run};

  bool setup_output(Output &output);
  void process(Output &output);
  void render_click(float *samples, uint32_t frames, uint32_t sample_rate,
                    uint64_t cycle_start_nsec,
                    const SyncSnapshot &snapshot) const;
  void render_reset(float *samples, uint32_t frames, uint32_t sample_rate,
                    uint64_t cycle_start_nsec,
                    const SyncSnapshot &snapshot) const;
  void render_run(float *samples, uint32_t frames, uint32_t sample_rate,
                  uint64_t cycle_start_nsec,
                  const SyncSnapshot &snapshot) const;
  void render_pulse(float *samples, uint32_t frames, uint32_t sample_rate,
                    uint64_t cycle_start_nsec, uint64_t pulse_nsec,
                    uint64_t max_duration_nsec) const;

  [[nodiscard]] std::string node_name(OutputKind kind) const;
  [[nodiscard]] std::string description(OutputKind kind) const;
};

} // namespace sesync
