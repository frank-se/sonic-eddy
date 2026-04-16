#include "controllers/CatchUpHandler.h"

#include "logging/log.h"

bool controllers::CatchUpHandler::should_update(
    const float normalized_value, const float normalized_current_value) {
  logging::log<logging::LogLevel::Trace>("CatchUpHandler::should_update");

  const auto now = std::chrono::steady_clock::now();
  const auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(
      now - _last_update.load());

  logging::log<logging::LogLevel::Debug>("Duration since last update is {}",
                                         duration);

  _last_update.store(now);

  if (_catching_up) {
    auto caught_up = false;
    if (_catch_up_direction == CatchUpDirection::FromBelow) {
      logging::log<logging::LogLevel::Debug>(
          "Currently catching up from below, new: {}, old: {}",
          normalized_value, normalized_current_value);

      caught_up = normalized_value > normalized_current_value;
    } else {
      logging::log<logging::LogLevel::Debug>(
          "Currently catching up from above, new: {}, old: {}",
          normalized_value, normalized_current_value);

      caught_up = normalized_value < normalized_current_value;
    }

    if (caught_up) {
      logging::log<logging::LogLevel::Debug>(
          "Caught up, stopping catch up process");

      _catching_up = false;
    }

    return !_catching_up;
  }

  if (duration < _expected_movement_duration) {
    logging::log<logging::LogLevel::Debug>(
        "Not catching up, and last update was {} ago, which is less than {}, "
        "accepting update",
        duration, _expected_movement_duration);

    return true;
  }

  const auto absolute_distance =
      std::abs(normalized_value - normalized_current_value);

  if (absolute_distance > _normalized_catch_up_distance) {
    _catching_up = true;
    _catch_up_direction = normalized_value < normalized_current_value
                              ? CatchUpDirection::FromBelow
                              : CatchUpDirection::FromAbove;

    logging::log<logging::LogLevel::Debug>(
        "Moved difference {} is bigger than {}, and last update was long ago, "
        "starting catch up process from {}",
        absolute_distance, _normalized_catch_up_distance,
        _catch_up_direction == CatchUpDirection::FromBelow ? "below" : "above");

    return false;
  }

  logging::log<logging::LogLevel::Debug>(
      "Movement is small enough, no catch up necessary");

  return true;
}
