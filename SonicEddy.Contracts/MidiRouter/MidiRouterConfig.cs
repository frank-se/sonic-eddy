using ProtoBuf;

namespace SonicEddy.Contracts.MidiRouter;

[ProtoContract]
public sealed class MidiRouterConfig
{
    [ProtoMember(1)]
    public List<MidiRouteConfig> Routes { get; set; } = [];
}

[ProtoContract]
public sealed class MidiRouteConfig
{
    [ProtoMember(1)]
    public Guid Id { get; set; }

    [ProtoMember(2)]
    public MidiRoutePortConfig Source { get; set; } = new();

    [ProtoMember(3)]
    public MidiRoutePortConfig Target { get; set; } = new();

    [ProtoMember(4)]
    public List<int> DropChannels { get; set; } = [];

    [ProtoMember(5)]
    public List<MidiChannelMapConfig> ChannelMap { get; set; } = [];
}

[ProtoContract]
public sealed class MidiRoutePortConfig
{
    [ProtoMember(1)]
    public string NodeName { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string PortName { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string PortAlias { get; set; } = string.Empty;

    [ProtoMember(4)]
    public string Direction { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class MidiChannelMapConfig
{
    [ProtoMember(1)]
    public int From { get; set; }

    [ProtoMember(2)]
    public int To { get; set; }
}
