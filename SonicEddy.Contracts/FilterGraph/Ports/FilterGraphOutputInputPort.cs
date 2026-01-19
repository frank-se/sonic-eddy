using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterGraphOutputInputPort(
    [property: ProtoMember(1)] Guid Id,
    [property: ProtoMember(2)] string Name) 
{
    public FilterGraphOutputInputPort() : this(Guid.Empty, string.Empty)
    {
    }
};