using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterGraphLv2InputPort(
    [property: ProtoMember(1)] Guid Id,
    [property: ProtoMember(2)] string Name,
    [property: ProtoMember(3)] string Symbol)
{
    public FilterGraphLv2InputPort() : this(Guid.Empty, string.Empty,
        string.Empty)
    {
    }
}