using System.Collections.Generic;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;

namespace SonicEddy.Services.MixerServiceV2;

public record GroupChannel(
    string Name,
    ulong ChannelId,
    LoopbackModule InputLoopback,
    FilterChain? FilterChain,
    LoopbackModule OutputLoopback,
    List<LoopbackModule> SendLoopbacks,
    Node? OutputTargetObject);