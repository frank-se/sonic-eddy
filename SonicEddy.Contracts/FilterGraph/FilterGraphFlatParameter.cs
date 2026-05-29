using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterGraphFlatParameter(
    [property: ProtoMember(1)] Guid NodeId,
    [property: ProtoMember(2)] string Symbol,
    [property: ProtoMember(3)] string DisplayName,
    [property: ProtoMember(4)] float Default)
{
    public FilterGraphFlatParameter() : this(Guid.Empty, string.Empty, string.Empty, 0f) { }
}
