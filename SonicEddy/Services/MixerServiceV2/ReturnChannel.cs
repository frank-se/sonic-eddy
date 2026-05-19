using Fr.Sonic.Modules.Models;

namespace SonicEddy.Services.MixerServiceV2;

public record ReturnChannel(
    string Name,
    LoopbackModule InputLoopback,
    FilterChain? FilterChain,
    LoopbackModule OutputLoopback);
