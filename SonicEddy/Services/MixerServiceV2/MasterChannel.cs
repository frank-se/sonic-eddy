using System.Collections.Generic;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;
using SonicEddy.Contracts.FilterGraph;

namespace SonicEddy.Services.MixerServiceV2;

public record MasterChannel(
    string Name,
    ulong ChannelId,
    LoopbackModule InputLoopback,
    Looper PreFxLooper,
    FilterChain? FilterChain,
    FilterGraph? FilterGraph,
    Looper PostFxLooper,
    Node? OutputTargetObject)
{
    public Looper OutputLoopback => PostFxLooper;
}
