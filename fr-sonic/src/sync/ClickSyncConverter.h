#pragma once

#include "SyncClient.h"

#include <cstdint>
#include <memory>
#include <pipewire/loop.h>
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

private:
  enum class OutputKind { Click, Reset, Run };
  class OutputProducer;

  pw_loop *_loop;
  ClickSyncConfig _config;
  std::shared_ptr<SyncClient> _sync_client;
  std::unique_ptr<OutputProducer> _click_output;
  std::unique_ptr<OutputProducer> _reset_output;
  std::unique_ptr<OutputProducer> _run_output;
};

} // namespace sesync
