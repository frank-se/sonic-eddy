using System.Collections.Generic;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Modules.Models;

namespace SonicEddy.Services.MixerServiceV2;

public record MasterChannel(
    string Name,
    ulong ChannelId,
    LoopbackModule InputLoopback,
    FilterChain? FilterChain,
    LoopbackModule OutputLoopback,
    Node? OutputTargetObject);