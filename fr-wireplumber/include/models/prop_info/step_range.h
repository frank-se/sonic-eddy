#pragma once

#include <expected>
#include <string>
#include <format>
#include <variant>

namespace models::prop_info {
template <std::arithmetic T>
class StepRange {
public:
  StepRange(T default_, T minimum, T maximum, T step)
    : default_(std::move(default_)), minimum(std::move(minimum)),
      maximum(std::move(maximum)), step(std::move(step)) {}

  StepRange() = default;

  T default_;
  T minimum;
  T maximum;
  T step;

  [[nodiscard]] std::expected<std::string, std::string> to_json() const {
    if constexpr (std::same_as<T, int>) {
      return std::format(R"(
      {{
        "type": "IntegerRange",
        "default": {},
        "minimum": {},
        "maximum": {},
        "step": {}
      }})", default_, minimum, maximum, step);
    }

    if constexpr (std::same_as<T, long>) {
      return std::format(R"(
      {{
        "type": "LongRange",
        "default": {},
        "minimum": {},
        "maximum": {},
        "step": {}
      }})", default_, minimum, maximum, step);
    }

    if constexpr (std::same_as<T, float>) {
      return std::format(R"(
      {{
        "type": "FloatRange",
        "default": {:.9g},
        "minimum": {:.9g},
        "maximum": {:.9g},
        "step": {:.9g}
      }})", default_, minimum, maximum, step);
    }

    if constexpr (std::same_as<T, double>) {
      return std::format(R"(
      {{
        "type": "DoubleRange",
        "default": {:.17g},
        "minimum": {:.17g},
        "maximum": {:.17g},
        "step": {:.17g}
      }})", default_, minimum, maximum, step);
    }

    return std::unexpected("Unknown step range type");
  }
};

using FloatStepRange = StepRange<float>;
using DoubleStepRange = StepRange<double>;
using IntegerStepRange = StepRange<int32_t>;
using LongStepRange = StepRange<int64_t>;

[[nodiscard]] std::expected<
  std::variant<IntegerStepRange, LongStepRange, FloatStepRange, DoubleStepRange>
  , std::string> inline step_range_from_spa_choice_values(spa_pod *values) {
  if (values->type == SPA_TYPE_Float) {
    const auto values_array = static_cast<float*>(SPA_POD_BODY(values));
    return FloatStepRange(values_array[0], values_array[1], values_array[2],
                          values_array[3]);
  } else if (values->type == SPA_TYPE_Double) {
    const auto values_array = static_cast<double*>(SPA_POD_BODY(values));
    return DoubleStepRange(values_array[0], values_array[1], values_array[2],
                           values_array[3]);
  } else if (values->type == SPA_TYPE_Int) {
    const auto values_array = static_cast<std::int32_t*>(SPA_POD_BODY(values));
    return IntegerStepRange(values_array[0], values_array[1], values_array[2],
                            values_array[3]);
  } else if (values->type == SPA_TYPE_Long) {
    const auto values_array = static_cast<std::int64_t*>(SPA_POD_BODY(values));
    return LongStepRange(values_array[0], values_array[1], values_array[2],
                         values_array[3]);
  }

  return std::unexpected("Unknown range type");
}
}

