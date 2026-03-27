#pragma once
#include <atomic>
#include <string>

namespace controllers {

struct Parameter {
  std::string name;
  float value;
  float max;
  float min;
};

} // namespace controllers
