using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterGraphInput(
    Guid Id,
    [property: ProtoMember(1)] List<FilterGraphInputOutputPort> OutputPorts)
    : FilterGraphNodeBase(Id, "Input")
{
    public FilterGraphInput() : this(Guid.Empty, [])
    {
    }
}