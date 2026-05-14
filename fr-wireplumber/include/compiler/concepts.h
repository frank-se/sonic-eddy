#pragma once

#include <concepts>

#if !defined(__cpp_lib_arithmetic_concept) || __cpp_lib_concepts < 202403L
namespace std {
template <class T>concept arithmetic = integral<T> || floating_point<T>;
}
#endif

template<class T, class... Ts>
inline constexpr bool is_any_of_v = (std::same_as<T, Ts> || ...);

template<class T, class... Ts>
concept is_any_of = is_any_of_v<T, Ts...>;
