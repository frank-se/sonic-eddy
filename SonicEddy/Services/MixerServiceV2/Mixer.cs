using System.Collections.Generic;

namespace SonicEddy.Services.MixerServiceV2;

public record Mixer(
    string Name,
    List<ChannelStrip> Channels,
    List<ReturnChannel> SendReturns,
    List<InputChannel> Inputs,
    List<OutputChannel> Outputs);