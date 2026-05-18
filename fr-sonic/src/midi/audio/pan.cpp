#include "audio/pan.h"

#include <complex>
#include <ranges>

constexpr float Boost = std::sqrt(2);

audio::pan::PanAndVolume
audio::pan::get_pan_and_volume_from_gains(const std::array<float, 2> gains) {
  if (gains[0] == 0.0f && gains[1] == 0.0f) {
    return PanAndVolume{
        .pan = 0.0f,
        .volume = 0.0f,
    };
  }

  auto internal =
      gains | std::views::transform([](auto gain) { return gain / Boost; });

  const auto volume =
      std::sqrt(internal[0] * internal[0] + internal[1] * internal[1]);
  const auto pan =
      std::atan2(internal[0], internal[1]) / (std::numbers::pi / 2.0f);

  return PanAndVolume{
      .pan = static_cast<float>(pan * 2 - 1),
      .volume = volume,
  };
}

std::array<float, 2>
audio::pan::get_gains_from_pan_and_volume(const PanAndVolume panAndVol) {
  auto pan = panAndVol.pan;
  auto volume = panAndVol.volume;

  auto angle = (pan + 1.0) * (std::numbers::pi / 4.0f);
  auto left = std::cos(angle);
  auto right = std::sin(angle);
  return {static_cast<float>(volume * left) * Boost,
          static_cast<float>(volume * right) * Boost};
}
