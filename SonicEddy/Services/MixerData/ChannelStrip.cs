using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Modules.Models;

namespace SonicEddy.Services.MixerData;

public record ChannelStrip(
    string Name,
    ulong ChannelId,
    Node InputNode,
    FilterChain? FilterModule,
    LoopbackModule LoopbackModule);