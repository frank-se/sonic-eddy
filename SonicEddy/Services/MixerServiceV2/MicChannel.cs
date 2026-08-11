using Fr.Sonic.Modules.Models;

namespace SonicEddy.Services.MixerServiceV2;

public record MicChannel(
    LoopbackModule InputLoopback,
    InsertProcessor? InsertProcessor)
{
    public FilterChain? FilterChain => InsertProcessor?.FilterChain;
    public Contracts.FilterGraph.FilterGraph? FilterGraph =>
        InsertProcessor?.FilterGraph;
}
