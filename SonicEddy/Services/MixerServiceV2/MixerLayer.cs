using System.Collections.Generic;

namespace SonicEddy.Services.MixerServiceV2;

public record MixerLayer(
    ulong layerId,
    string Name,
    MasterChannel MasterChannel,
    List<GroupChannel> GroupChannels,
    List<ChannelStrip> Channels,
    List<ReturnChannel> SendReturns,
    List<InputChannel> Inputs,
    List<OutputChannel> Outputs);