using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Modules.Models;
using SonicEddy.Contracts.FilterGraph;

namespace SonicEddy.Services.MixerData;

public record ChannelStrip(
    string Name,
    ulong ChannelId,
    Node InputNode,
    FilterChain? FilterModule,
    LoopbackModule LoopbackModule);