using System.Collections.Generic;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;

namespace SonicEddy.Services.MixerServiceV2;

public record MasterChannel(
    string Name,
    ulong ChannelId,
    TwoNodePipewireModule InputLoopback,
    Looper PreFxLooper,
    InsertProcessor? InsertProcessor,
    Looper PostFxLooper,
    Node? OutputTargetObject)
{
    public Looper OutputLoopback => PostFxLooper;
    public FilterChain? FilterChain => InsertProcessor?.FilterChain;
    public Contracts.FilterGraph.FilterGraph? FilterGraph =>
        InsertProcessor?.FilterGraph;
}
