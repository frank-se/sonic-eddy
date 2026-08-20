using ProtoBuf;

namespace SonicEddy.Contracts.CameraRouter;

[ProtoContract]
public sealed class CameraRouterConfig
{
    [ProtoMember(1)]
    public List<CameraSlotConfig> Slots { get; set; } = [];
}

[ProtoContract]
public sealed class CameraSlotConfig
{
    [ProtoMember(1)]
    public int SlotIndex { get; set; }

    [ProtoMember(2)]
    public string SourceNodeName { get; set; } = string.Empty;
}
