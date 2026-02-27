using Fr.Wireplumber.Model.Objects;

namespace SonicEddy.Services.MixerServiceV2;

public record OutputChannel(
    string Name,
    Node CaptureNode);