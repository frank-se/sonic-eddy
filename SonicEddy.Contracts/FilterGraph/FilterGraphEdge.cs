using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterGraphEdge(
    [property: ProtoMember(1)] Guid Source,
    [property: ProtoMember(2)] Guid Target)
{
    public FilterGraphEdge() : this(Guid.Empty, Guid.Empty)
    {
    }
}