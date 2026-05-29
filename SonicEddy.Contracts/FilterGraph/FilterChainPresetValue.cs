using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterChainPresetValue(
    [property: ProtoMember(1)] string FullyQualifiedName,
    [property: ProtoMember(2)] float Value)
{
    public FilterChainPresetValue() : this(string.Empty, 0f) { }
}
