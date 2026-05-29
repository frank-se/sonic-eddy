using System;
using System.Collections.Generic;
using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterChainPreset(
    [property: ProtoMember(1)] Guid Id,
    [property: ProtoMember(2)] string Name,
    [property: ProtoMember(3)] Guid FilterGraphId,
    [property: ProtoMember(4)] List<FilterChainPresetValue> Values)
{
    public FilterChainPreset() : this(Guid.Empty, string.Empty, Guid.Empty, []) { }
}
