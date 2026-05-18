#pragma once
#include <array>

namespace audio::pan {

struct PanAndVolume {
  float pan;
  float volume;
};

PanAndVolume get_pan_and_volume_from_gains(std::array<float, 2> gains);

std::array<float, 2> get_gains_from_pan_and_volume(PanAndVolume panAndVol);

} // namespace audio::pan
