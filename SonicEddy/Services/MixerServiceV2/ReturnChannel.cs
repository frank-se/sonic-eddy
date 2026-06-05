using Fr.Sonic.Modules.Models;
using SonicEddy.Contracts.FilterGraph;

namespace SonicEddy.Services.MixerServiceV2;

public record ReturnChannel(
    string Name,
    LoopbackModule InputLoopback,
    FilterChain? FilterChain,
    FilterGraph? FilterGraph,
    LoopbackModule OutputLoopback);
