using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterGraphBuiltinControl(
    [property: ProtoMember(1)] string Name,
    [property: ProtoMember(2)] double Value)
{
    public FilterGraphBuiltinControl() : this(string.Empty, 0) { }
}
