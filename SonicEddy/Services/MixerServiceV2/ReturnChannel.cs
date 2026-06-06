using Fr.Sonic.Modules.Models;

namespace SonicEddy.Services.MixerServiceV2;

public record ReturnChannel(
    string Name,
    LoopbackModule InputLoopback,
    InsertProcessor? InsertProcessor,
    LoopbackModule OutputLoopback)
{
    public FilterChain? FilterChain => InsertProcessor?.FilterChain;
    public Contracts.FilterGraph.FilterGraph? FilterGraph =>
        InsertProcessor?.FilterGraph;
}
