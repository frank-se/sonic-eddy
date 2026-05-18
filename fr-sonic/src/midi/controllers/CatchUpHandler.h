#pragma once
#include <atomic>
#include <chrono>

namespace controllers {

class CatchUpHandler {
public:
  bool should_update(float normalized_value, float normalized_current_value);

private:
  enum class CatchUpDirection { FromAbove, FromBelow };

  static constexpr float _normalized_catch_up_distance = 0.02f;
  static constexpr std::chrono::milliseconds _expected_movement_duration{200};

  std::atomic<std::chrono::steady_clock::time_point> _last_update{
      std::chrono::steady_clock::now()};

  std::atomic<bool> _catching_up{false};

  CatchUpDirection _catch_up_direction{CatchUpDirection::FromBelow};
};

} // namespace controllers
