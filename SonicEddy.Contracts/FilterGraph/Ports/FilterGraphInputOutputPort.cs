using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterGraphInputOutputPort(
    [property: ProtoMember(1)] Guid Id,
    [property: ProtoMember(2)] string Name)
{
    public FilterGraphInputOutputPort() : this(Guid.Empty, string.Empty)
    {
    }
}