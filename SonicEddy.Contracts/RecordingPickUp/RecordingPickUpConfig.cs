using ProtoBuf;

namespace SonicEddy.Contracts.RecordingPickUp;

[ProtoContract]
public sealed class RecordingPickUpConfig
{
    [ProtoMember(1)]
    public List<RecordingPickUpEntry> PickUps { get; set; } = [];
}

[ProtoContract]
public sealed class RecordingPickUpEntry
{
    [ProtoMember(1)]
    public Guid Id { get; set; }

    [ProtoMember(2)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(3)]
    public bool IsActive { get; set; } = true;

    [ProtoMember(4)]
    public string DestNodeName { get; set; } = string.Empty;

    [ProtoMember(5)]
    public double Trim { get; set; }

    // Structured source: identifies a point in the mixer signal chain.
    // SourceChannelType maps to MonitoringChannelType (Strip=0, Group=1, Master=2, Return=3).
    // SourcePickUpPosition maps to MonitoringSource (Pre=1, Post=2, OutPreFader=3, OutPostFader=4).
    [ProtoMember(6)]
    public int SourceLayerIndex { get; set; }

    [ProtoMember(7)]
    public int SourceChannelType { get; set; }

    [ProtoMember(8)]
    public int SourceChannelIndex { get; set; }

    [ProtoMember(9)]
    public int SourcePickUpPosition { get; set; }
}
