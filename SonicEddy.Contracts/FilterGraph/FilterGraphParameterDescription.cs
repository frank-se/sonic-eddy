using ProtoBuf;
using SonicEddy.Contracts.Parameters;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterGraphParameterDescription(
    [property: ProtoMember(1)] string Name,
    [property: ProtoMember(2)] string DisplayName,
    [property: ProtoMember(3)] float Min,
    [property: ProtoMember(4)] float Max,
    [property: ProtoMember(5)] float Default)
{
    public FilterGraphParameterDescription() : this(string.Empty, string.Empty,
        0.0f, 0.0f, 0.0f)
    {
    }
};