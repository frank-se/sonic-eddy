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
    public string SourceNodeName { get; set; } = string.Empty;

    [ProtoMember(5)]
    public string DestNodeName { get; set; } = string.Empty;

    [ProtoMember(6)]
    public double Trim { get; set; }
}
