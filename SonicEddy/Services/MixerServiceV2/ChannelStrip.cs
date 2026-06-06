using System;
using System.Collections.Generic;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;

namespace SonicEddy.Services.MixerServiceV2;

public record ChannelStrip(
    string Name,
    ulong ChannelId,
    TwoNodePipewireModule InputLoopback,
    Looper PreFxLooper,
    InsertProcessor? InsertProcessor,
    Looper PostFxLooper,
    List<LoopbackModule> SendLoopbacks,
    Node? InputTargetObject,
    Node? OutputTargetObject,
    IntPtr SilenceProducerHandle)
{
    public Looper OutputLoopback => PostFxLooper;
    public FilterChain? FilterChain => InsertProcessor?.FilterChain;
    public Contracts.FilterGraph.FilterGraph? FilterGraph =>
        InsertProcessor?.FilterGraph;
}
