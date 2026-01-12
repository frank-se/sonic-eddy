using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterGraphInput(
    Guid Id,
    [property: ProtoMember(1)] List<FilterGraphOutputPort> OutputPorts)
    : FilterGraphNodeBase(Id, "Input")
{
    public FilterGraphInput() : this(Guid.Empty, [])
    {
    }
}