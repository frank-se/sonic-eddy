using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterGraphLv2Plugin(
    Guid Id,
    string Name,
    [property: ProtoMember(1)] List<FilterGraphInputPort> InputPorts,
    [property: ProtoMember(2)] List<FilterGraphOutputPort> OutputPorts)
    : FilterGraphNodeBase(Id, Name)
{
    public FilterGraphLv2Plugin() : this(Guid.Empty, string.Empty, [], [])
    {
    }
}