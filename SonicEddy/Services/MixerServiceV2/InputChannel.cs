using Fr.Wireplumber.Model.Objects;

namespace SonicEddy.Services.MixerServiceV2;

public record InputChannel(
    string Name,
    Node PlaybackNode);