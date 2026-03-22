#pragma once
#include "midi/Messages.h"

#include <cstdint>
#include <functional>
#include <utility>

namespace midi::action_container {

using NoteOnCallback =
    std::function<void(size_t port_id, size_t mapping_id, uint64_t note_number,
                       uint64_t velocity)>;

class NoteOnCallbackBehavior {
public:
  NoteOnCallbackBehavior(const size_t port_id, const size_t mapping_id,
                         NoteOnCallback callback)
      : _port_id(port_id), _mapping_id(mapping_id),
        _callback(std::move(callback)) {}

  void process(const NoteOnMessages &control_change);

private:
  const size_t _port_id;
  const size_t _mapping_id;

  NoteOnCallback _callback;
};

} // namespace midi::action_container
