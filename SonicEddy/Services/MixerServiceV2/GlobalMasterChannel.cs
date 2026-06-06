using Fr.Sonic.Modules.Models;
using Fr.Sonic.Model.Objects;

namespace SonicEddy.Services.MixerServiceV2;

public record GlobalMasterChannel(FilterChain CrossFader,
    Node OutputTargetObject,
    InsertProcessor? InsertProcessor = null)
{
    public FilterChain? FilterChain => InsertProcessor?.FilterChain;
    public Contracts.FilterGraph.FilterGraph? FilterGraph =>
        InsertProcessor?.FilterGraph;
}
