using System.Collections.Generic;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Modules.Models;
using SonicEddy.Contracts.FilterGraph;

namespace SonicEddy.Services.MixerServiceV2;

public record MasterChannel(
    string Name,
    ulong ChannelId,
    LoopbackModule InputLoopback,
    FilterChain? FilterChain,
    FilterGraph? FilterGraph,
    LoopbackModule OutputLoopback,
    Node? OutputTargetObject);