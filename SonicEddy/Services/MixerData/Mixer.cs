using System.Collections.ObjectModel;

namespace SonicEddy.Services.MixerData;

public record Mixer(
    string Name,
    ReadOnlyCollection<ChannelStrip> ChannelStrips);