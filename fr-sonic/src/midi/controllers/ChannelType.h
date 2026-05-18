#pragma once

#include <ostream>

namespace controllers {

enum class ChannelType { CHANNEL = 0, GROUP_CHANNEL = 1 };

}

inline std::ostream &operator<<(std::ostream &lhs,
                                const controllers::ChannelType rhs) {
  switch (rhs) {
  case controllers::ChannelType::CHANNEL:
    lhs << "CHANNEL";
    break;
  case controllers::ChannelType::GROUP_CHANNEL:
    lhs << "GROUP_CHANNEL";
    break;
  };

  return lhs;
}
