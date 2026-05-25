#pragma once

#include <format>
#include <iostream>
#include <string_view>

namespace logging {

enum class LogLevel { None = 0, Fatal, Error, Warning, Info, Debug, Trace };

inline constexpr auto LOG_LEVEL = LogLevel::Trace;

constexpr std::string_view to_string(const LogLevel level) noexcept {
  switch (level) {
  case LogLevel::None:
    return "None";
  case LogLevel::Fatal:
    return "Fatal";
  case LogLevel::Error:
    return "Error";
  case LogLevel::Warning:
    return "Warning";
  case LogLevel::Info:
    return "Info";
  case LogLevel::Debug:
    return "Debug";
  case LogLevel::Trace:
    return "Trace";
  default:
    return "Unknown";
  }
}

template <LogLevel level, typename... Args>
void log(std::format_string<Args...> fmt, Args &&...args) noexcept {
  if constexpr (level > LOG_LEVEL) {
    (void)fmt;
    (..., (void)args);
    return;
  }

  if constexpr (level == LogLevel::Fatal || level == LogLevel::Error) {
    std::print(std::cerr, "[{}] {}\n", to_string(level),
               std::format(fmt, std::forward<Args>(args)...));
    std::cerr << std::flush;
  } else {
    std::print(std::cout, "[{}] {}\n", to_string(level),
               std::format(fmt, std::forward<Args>(args)...));
    std::cout << std::flush;
  }
}

} // namespace logging
