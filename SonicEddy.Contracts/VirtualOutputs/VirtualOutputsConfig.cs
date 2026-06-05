using ProtoBuf;

namespace SonicEddy.Contracts.VirtualOutputs;

[ProtoContract]
public sealed class VirtualOutputsConfig
{
    [ProtoMember(1)]
    public List<VirtualOutputConfig> Outputs { get; set; } = [];
}

[ProtoContract]
public sealed class VirtualOutputConfig
{
    [ProtoMember(1)]
    public Guid Id { get; set; }

    [ProtoMember(2)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string NodeName { get; set; } = string.Empty;

    [ProtoMember(4)]
    public List<VirtualOutputPortConfig> Ports { get; set; } = [];
}

[ProtoContract]
public sealed class VirtualOutputPortConfig
{
    [ProtoMember(1)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Alias { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string Channel { get; set; } = string.Empty;
}
