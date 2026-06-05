using System;
using System.Collections.Generic;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;
using SonicEddy.Contracts.FilterGraph;

namespace SonicEddy.Services.MixerServiceV2;

public record GroupChannel(
    string Name,
    ulong ChannelId,
    TwoNodePipewireModule InputLoopback,
    Looper PreFxLooper,
    FilterChain? FilterChain,
    FilterGraph? FilterGraph,
    Looper PostFxLooper,
    List<LoopbackModule> SendLoopbacks,
    Node? OutputTargetObject,
    IntPtr SilenceProducerHandle)
{
    public Looper OutputLoopback => PostFxLooper;
}
