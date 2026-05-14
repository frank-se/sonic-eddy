#include "frmonitoring.h"

#include <iostream>
#include <ostream>
#include <unistd.h>

void callback(const uint64_t object_serial, const float left_peak,
              const float right_peak, const float left_average,
              const float right_average) {
  std::cout << object_serial << std::endl;

  std::cout << "left peak: " << left_peak << std::endl;
  std::cout << "left average: " << left_average << std::endl;

  std::cout << "right peak: " << right_peak << std::endl;
  std::cout << "right average " << right_average << std::endl;
}

int main(int argc, char **argv) {
  init(callback, 500);
  start();
  start_monitor_node(1975);
  sleep(200000);
  stop();
}
