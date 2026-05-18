#pragma once

#include "DialMode.h"
#include "ChannelType.h"

#include <cstdint>
#include <functional>

namespace controllers {

using LayerSelectCallback = std::function<void(size_t layer_id)>;

using ChannelSelectCallback = std::function<void(size_t channel_id)>;

using DialSectionModeCallback =
    std::function<void(size_t channel_id, DialMode)>;

using FilterParamsSectionSelectCallback =
    std::function<void(size_t channel_id, size_t section_id)>;

using FilterParamsSectionMovePagesRightCallback =
    std::function<void(uint64_t step_count)>;

using FilterParamsSectionMovePagesLeftCallback =
    std::function<void(uint64_t step_count)>;

using MidiControlChangeUpdateCallback = std::function<void(
    ChannelType channel_type, ulong channel_id, ulong object_id,
    const char *parameter_name, float normalized_controller_value,
    float normalized_known_value, bool catching_up)>;

} // namespace controllers
