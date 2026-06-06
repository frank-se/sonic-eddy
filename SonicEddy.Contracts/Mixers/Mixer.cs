using ProtoBuf;
using SonicEddy.Contracts.FilterGraph;

namespace SonicEddy.Contracts.Mixers;

[ProtoContract]
public sealed class Mixer
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public string Name { get; set; } = string.Empty;
    [ProtoMember(3)] public List<MixerLayerConfig> Layers { get; set; } = [];
    [ProtoMember(4)] public CrossFaderConfig MainCrossFader { get; set; } = new();
    [ProtoMember(5)] public CrossFaderConfig CueCrossFader { get; set; } = new();
    [ProtoMember(6)] public CueChannelConfig Cue { get; set; } = new();
    [ProtoMember(7)] public InsertProcessorConfig? GlobalMasterInsert { get; set; }
}

[ProtoContract]
public sealed class MixerLayerConfig
{
    [ProtoMember(1)] public ulong LayerId { get; set; }
    [ProtoMember(2)] public List<ChannelConfig> Channels { get; set; } = [];
    [ProtoMember(3)] public List<ChannelConfig> Groups { get; set; } = [];
    [ProtoMember(4)] public ChannelConfig Master { get; set; } = new();
    [ProtoMember(5)] public List<ReturnChannelConfig> Returns { get; set; } = [];
}

[ProtoContract]
public sealed class ChannelConfig
{
    [ProtoMember(1)] public ulong ChannelId { get; set; }
    [ProtoMember(2)] public string? AudioFromNodeName { get; set; }
    [ProtoMember(3)] public string? AudioToNodeName { get; set; }
    [ProtoMember(4)] public InsertProcessorConfig? InsertProcessor { get; set; }
    [ProtoMember(5)] public double Pan { get; set; }
    [ProtoMember(6)] public double Volume { get; set; } = 1.0;
    [ProtoMember(7)] public List<double> Sends { get; set; } = [];
    [ProtoMember(8)] public double? Trim { get; set; }
    [ProtoMember(9)] public LooperConfig Looper { get; set; } = new();
}

[ProtoContract]
public sealed class ReturnChannelConfig
{
    [ProtoMember(1)] public int Index { get; set; }
    [ProtoMember(2)] public InsertProcessorConfig? InsertProcessor { get; set; }
    [ProtoMember(3)] public double Pan { get; set; }
    [ProtoMember(4)] public double Volume { get; set; } = 1.0;
}

[ProtoContract]
public sealed class InsertProcessorConfig
{
    [ProtoMember(1)] public InsertProcessorType Type { get; set; }
    [ProtoMember(2)] public Guid ProcessorId { get; set; }
    [ProtoMember(3)]
    public List<FilterChainPresetValue> Values { get; set; } = [];
}

public enum InsertProcessorType
{
    FilterChain = 1,
    ExternalEffect = 2
}

[ProtoContract]
public sealed class LooperConfig
{
    [ProtoMember(1)] public bool IsPostFxSelected { get; set; }
    [ProtoMember(2)] public double Mix { get; set; }
    [ProtoMember(3)] public int BarLength { get; set; } = 1;
    [ProtoMember(4)] public bool IsQuantized { get; set; } = true;
}

[ProtoContract]
public sealed class CrossFaderConfig
{
    [ProtoMember(1)] public double Position { get; set; }
    [ProtoMember(2)] public int Shape { get; set; }
    [ProtoMember(3)] public int Mode { get; set; }
}

[ProtoContract]
public sealed class CueChannelConfig
{
    [ProtoMember(1)] public string? AudioToNodeName { get; set; }
    [ProtoMember(2)] public double Pan { get; set; }
    [ProtoMember(3)] public double Volume { get; set; } = 1.0;
}
