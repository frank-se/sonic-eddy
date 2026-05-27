using System;
using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterGraphBuiltinPort(
    [property: ProtoMember(1)] Guid Id,
    [property: ProtoMember(2)] string Name)
{
    public FilterGraphBuiltinPort() : this(Guid.Empty, string.Empty) { }
}
