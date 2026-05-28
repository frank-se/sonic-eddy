using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterGraphLv2Plugin(
    Guid Id,
    string Name,
    [property: ProtoMember(1)] string Uri,
    [property: ProtoMember(2)] List<FilterGraphLv2InputPort> InputPorts,
    [property: ProtoMember(3)] List<FilterGraphLv2OutputPort> OutputPorts,
    [property: ProtoMember(4)] List<FilterGraphLv2Control> InitialControls)
    : FilterGraphNodeBase(Id, Name)
{
    public FilterGraphLv2Plugin() : this(Guid.Empty, string.Empty, string.Empty,
        [], [], [])
    {
    }
}