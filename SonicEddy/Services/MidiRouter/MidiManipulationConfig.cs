using System.Collections.Generic;

namespace SonicEddy.Services.MidiRouter;

public sealed record MidiManipulationConfig(
    IReadOnlyList<int> DropChannels,
    IReadOnlyList<ChannelMapEntry> ChannelMap)
{
    public static MidiManipulationConfig Passthrough { get; } = new([], []);

    public bool IsPassthrough =>
        DropChannels.Count == 0 && ChannelMap.Count == 0;
}

public sealed record ChannelMapEntry(int From, int To);
