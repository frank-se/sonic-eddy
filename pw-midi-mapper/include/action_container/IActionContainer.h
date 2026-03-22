#pragma once

#include "midi/Messages.h"

namespace action_container {

class IActionContainer {
public:
  virtual void process(const midi::Message &message) = 0;

  virtual ~IActionContainer() = default;
};

} // namespace action_container
