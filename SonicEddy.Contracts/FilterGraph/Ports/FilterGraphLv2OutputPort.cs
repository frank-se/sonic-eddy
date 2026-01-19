using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterGraphLv2OutputPort(
    [property: ProtoMember(1)] Guid Id,
    [property: ProtoMember(2)] string Name,
    [property: ProtoMember(3)] string Symbol)
{
    public FilterGraphLv2OutputPort() : this(Guid.Empty, string.Empty,
        string.Empty)
    {
    }
}