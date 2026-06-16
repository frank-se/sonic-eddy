using ProtoBuf;

namespace SonicEddy.Contracts.JackInputPorts;

[ProtoContract]
public sealed class JackInputPortsConfig
{
    [ProtoMember(1)]
    public List<JackInputPortConfig> Ports { get; set; } = [];
}

[ProtoContract]
public sealed class JackInputPortConfig
{
    [ProtoMember(1)]
    public Guid Id { get; set; }

    [ProtoMember(2)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// JACK client output port names in order; index maps to capture port index (FL, FR, ...).
    /// </summary>
    [ProtoMember(4)]
    public List<string> JackPortNames { get; set; } = [];
}
