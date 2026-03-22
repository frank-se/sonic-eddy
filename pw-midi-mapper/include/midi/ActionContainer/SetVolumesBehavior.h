#pragma once
#include "midi/Processor.h"

#include <pipewire/pipewire.h>

#include <atomic>
#include <cstdint>

namespace midi::action_container {

class SetVolumesBehavior {
public:
  explicit SetVolumesBehavior(const uint64_t object_id, pw_registry *registry,
                              pw_main_loop *loop)
      : _loop(loop), _registry(registry), _object_id(object_id) {}

  void bind();

  void setup_node_listener();

  void process(const ControlChangeMessages &control_change);

  void set_pan(const float pan) { _pan = pan; }

  void set_volume(const float volume) { _volume = volume; }

private:
  pw_main_loop *_loop;
  pw_registry *_registry;

  uint64_t _object_id;
  pw_node *_node = nullptr;
  spa_hook _node_listener{};

  std::atomic<float> _pan = 0.0f;
  std::atomic<float> _volume = 0.0f;
};

} // namespace midi::action_container
