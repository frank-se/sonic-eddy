#pragma once

#include <expected>
#include <string>
#include <format>
#include <variant>

#include <spa/pod/pod.h>

namespace models::prop_info {
template <std::arithmetic T>
class Range {
public:
  Range(T default_, T minimum, T maximum)
    : default_(std::move(default_)), minimum(std::move(minimum)),
      maximum(std::move(maximum)) {}

  Range() = default;

  T default_;
  T minimum;
  T maximum;

  [[nodiscard]] std::expected<std::string, std::string> to_json() const {
    if constexpr (std::same_as<T, int>) {
      return std::format(R"(
      {{
        "type": "IntegerRange",
        "default": {},
        "minimum": {},
        "maximum": {}
      }})", default_, minimum, maximum);
    }

    if constexpr (std::same_as<T, long>) {
      return std::format(R"(
      {{
        "type": "LongRange",
        "default": {},
        "minimum": {},
        "maximum": {}
      }})", default_, minimum, maximum);
    }

    if constexpr (std::same_as<T, float>) {
      return std::format(R"(
      {{
        "type": "FloatRange",
        "default": {:.9g},
        "minimum": {:.9g},
        "maximum": {:.9g}
      }})", default_, minimum, maximum);
    }

    if constexpr (std::same_as<T, double>) {
      return std::format(R"(
      {{
        "type": "DoubleRange",
        "default": {:.17g},
        "minimum": {:.17g},
        "maximum": {:.17g}
      }})", default_, minimum, maximum);
    }

    return std::unexpected("Unknown range type");
  }
};

using FloatRange = Range<float>;
using DoubleRange = Range<double>;
using IntegerRange = Range<int32_t>;
using LongRange = Range<int64_t>;

[[nodiscard]] std::expected<std::variant<IntegerRange, LongRange, FloatRange, DoubleRange>,
                            std::string> inline range_from_spa_choice_values(
  spa_pod *values) {
  if (values->type == SPA_TYPE_Float) {
    const auto values_array = static_cast<float*>(SPA_POD_BODY(values));
    return FloatRange(values_array[0], values_array[1], values_array[2]);
  } else if (values->type == SPA_TYPE_Double) {
    const auto values_array = static_cast<double*>(SPA_POD_BODY(values));
    return DoubleRange(values_array[0], values_array[1], values_array[2]);
  } else if (values->type == SPA_TYPE_Int) {
    const auto values_array = static_cast<std::int32_t*>(SPA_POD_BODY(values));
    return IntegerRange(values_array[0], values_array[1], values_array[2]);
  } else if (values->type == SPA_TYPE_Long) {
    const auto values_array = static_cast<std::int64_t*>(SPA_POD_BODY(values));
    return LongRange(values_array[0], values_array[1], values_array[2]);
  }

  return std::unexpected("Unknown range type");
}
}
