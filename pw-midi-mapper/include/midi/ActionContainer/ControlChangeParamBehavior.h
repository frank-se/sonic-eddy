#pragma once

#include "midi/Messages.h"

#include <atomic>
#include <pipewire/pipewire.h>

#include <string>

namespace midi::action_container {

struct set_params_data {
  constexpr static size_t size = 128;
  std::uint8_t buffer[size]{};
  spa_pod *pod = nullptr;
  pw_node *node = nullptr;
};

class ControlChangeParamBehavior {
public:
  explicit ControlChangeParamBehavior(const uint64_t object_id,
                                      pw_registry *registry, pw_main_loop *loop,
                                      std::string param_name, const float min,
                                      const float max)
      : _loop(loop), _registry(registry), _object_id(object_id),
        _parameter_name(std::move(param_name)), _min(min), _max(max) {}

  void bind();

  void setup_node_listener();

  void process(const ControlChangeMessages &control_change);

  void set_value(const float value) { _value = value; }

  std::string &parameter_name() { return _parameter_name; }

private:
  pw_main_loop *_loop;
  pw_registry *_registry;

  uint64_t _object_id;
  pw_node *_node = nullptr;
  spa_hook _node_listener{};

  std::string _parameter_name;

  std::atomic<float> _value = 0.0f;
  const float _min;
  const float _max;

  void build_set_params_pod(float value, set_params_data &data) const;
};

} // namespace midi::action_container
