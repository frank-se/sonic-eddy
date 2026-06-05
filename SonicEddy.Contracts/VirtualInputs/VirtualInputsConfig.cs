using ProtoBuf;

namespace SonicEddy.Contracts.VirtualInputs;

[ProtoContract]
public sealed class VirtualInputsConfig
{
    [ProtoMember(1)]
    public List<VirtualInputConfig> Inputs { get; set; } = [];
}

[ProtoContract]
public sealed class VirtualInputConfig
{
    [ProtoMember(1)]
    public Guid Id { get; set; }

    [ProtoMember(2)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string NodeName { get; set; } = string.Empty;

    [ProtoMember(4)]
    public List<VirtualInputPortConfig> Ports { get; set; } = [];
}

[ProtoContract]
public sealed class VirtualInputPortConfig
{
    [ProtoMember(1)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Alias { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string Channel { get; set; } = string.Empty;
}
