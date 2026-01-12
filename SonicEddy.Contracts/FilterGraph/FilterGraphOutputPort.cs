using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterGraphOutputPort(
    Guid Id,
    [property: ProtoMember(1)] string Name) : FilterGraphPortBase(Id)
{
    public FilterGraphOutputPort() : this(Guid.Empty, string.Empty)
    {
    }
}