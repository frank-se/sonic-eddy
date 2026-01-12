using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterGraph(
    [property: ProtoMember(1)] Guid Id,
    [property: ProtoMember(2)] string Name,
    [property: ProtoMember(3)] List<FilterGraphNodeBase> Nodes,
    [property: ProtoMember(4)] List<FilterGraphEdge> Edges)
{
    public FilterGraph() : this(Guid.Empty, string.Empty, [], [])
    {
    }
}