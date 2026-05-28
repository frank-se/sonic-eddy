using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterGraphLv2Control(
    [property: ProtoMember(1)] string Symbol,
    [property: ProtoMember(2)] float Value)
{
    public FilterGraphLv2Control() : this(string.Empty, 0f) { }
}
