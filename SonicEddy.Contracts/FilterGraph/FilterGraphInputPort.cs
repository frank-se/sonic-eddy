using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterGraphInputPort(
    Guid Id,
    [property: ProtoMember(1)] string Name) : FilterGraphPortBase(Id)
{
    public FilterGraphInputPort() : this(Guid.Empty, string.Empty)
    {
    }
}