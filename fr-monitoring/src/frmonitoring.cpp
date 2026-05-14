#include "frmonitoring.h"

#include "monitor/monitor.h"

#include <memory>

std::shared_ptr<monitor::Monitor> g_monitor = nullptr;

void init(peak_callback_pointer_t callback,
          uint64_t update_interval_milliseconds) {
  g_monitor = std::make_shared<monitor::Monitor>(callback,
                                                 update_interval_milliseconds);
}

void start() {
  g_monitor->start();
}

void stop() {
  g_monitor->stop();
}

void start_monitor_node(const uint64_t object_serial) {
  g_monitor->start_monitor_node(object_serial);
}

void stop_monitor_node(const uint64_t object_serial) {
  g_monitor->stop_monitor_node(object_serial);
}
