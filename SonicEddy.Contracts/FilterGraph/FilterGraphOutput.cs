using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterGraphOutput(
    Guid Id,
    [property: ProtoMember(1)] List<FilterGraphInputPort> InputPorts)
    : FilterGraphNodeBase(Id, "Output")
{
    public FilterGraphOutput() : this(Guid.Empty, [])
    {
    }
}