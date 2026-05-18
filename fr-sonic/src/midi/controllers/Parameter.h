#pragma once
#include "CatchUpHandler.h"

#include <atomic>
#include <string>

namespace controllers {

struct Parameter {
  std::string name;
  float max;
  float min;
  std::shared_ptr<CatchUpHandler> catch_up_handler =
      std::make_shared<CatchUpHandler>();
};

} // namespace controllers
