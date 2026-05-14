#include "frlv2.h"

#include <iostream>
#include <ostream>

int main(int argc, char **argv) {
  init();
  const auto result = plugin_descriptions_json();
  // const auto result = plugin_classes_json();
  std::cout << result << std::endl;
  destroy();
}
