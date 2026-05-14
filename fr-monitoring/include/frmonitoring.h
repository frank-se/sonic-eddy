#pragma once

#define FR_MONITORING_API __attribute__((visibility("default")))

#include <cstdint>

using peak_callback_pointer_t = void (*)(uint64_t object_serial,
                                         float left_peak, float right_peak,
                                         float left_average,
                                         float right_average);

extern "C" {
FR_MONITORING_API void init(peak_callback_pointer_t callback,
                            uint64_t update_interval_milliseconds);

FR_MONITORING_API void start();
FR_MONITORING_API void stop();

FR_MONITORING_API void start_monitor_node(uint64_t object_serial);
FR_MONITORING_API void stop_monitor_node(uint64_t object_serial);
}
