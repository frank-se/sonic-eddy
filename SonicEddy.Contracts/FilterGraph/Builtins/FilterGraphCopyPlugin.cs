using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph.Builtins;

public record FilterGraphCopyPlugin(
    Guid Id,
    string Name,
    [property: ProtoMember(1)] List<FilterGraphInputPort> InputPorts,
    [property: ProtoMember(2)] List<FilterGraphOutputPort> OutputPorts)
    : FilterGraphNodeBase(Id, Name)
{
    public FilterGraphCopyPlugin() : this(Guid.Empty, string.Empty, [], [])
    {
    }

    public FilterGraphCopyPlugin(Guid Id, string Name) : this(
        Id,
        Name,
        [new(Guid.NewGuid(), "In")],
        [new(Guid.NewGuid(), "Out")])
    {
    }
}