using ProtoBuf;

namespace SonicEddy.Contracts.ClickSync;

[ProtoContract]
public sealed class ClickSyncConfig
{
    [ProtoMember(1)]
    public List<ClickSyncConverterConfig> Converters { get; set; } = [];
}

[ProtoContract]
public sealed class ClickSyncConverterConfig
{
    [ProtoMember(1)]
    public Guid Id { get; set; }

    [ProtoMember(2)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(3)]
    public uint PulsesPerQuarterNote { get; set; } = 24;

    [ProtoMember(4)]
    public double PulseLengthMs { get; set; } = 5.0;

    [ProtoMember(5)]
    public float PulseAmplitude { get; set; } = 0.75f;

    [ProtoMember(6)]
    public List<ClickSyncTargetPortConfig> ClickTargets { get; set; } = [];

    [ProtoMember(7)]
    public List<ClickSyncTargetPortConfig> ResetTargets { get; set; } = [];

    [ProtoMember(8)]
    public List<ClickSyncTargetPortConfig> RunTargets { get; set; } = [];
}

[ProtoContract]
public sealed class ClickSyncTargetPortConfig
{
    [ProtoMember(1)]
    public string NodeName { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string PortName { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string PortAlias { get; set; } = string.Empty;
}
