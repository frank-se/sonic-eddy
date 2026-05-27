using System;
using System.Collections.Generic;
using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterGraphBuiltinNode(
    Guid Id,
    string Name,
    [property: ProtoMember(1)] BuiltinNodeType NodeType,
    [property: ProtoMember(2)] int ChannelCount,
    [property: ProtoMember(3)] List<FilterGraphBuiltinPort> InputPorts,
    [property: ProtoMember(4)] List<FilterGraphBuiltinPort> OutputPorts,
    [property: ProtoMember(5)] List<FilterGraphBuiltinControl> InitialControls)
    : FilterGraphNodeBase(Id, Name)
{
    public FilterGraphBuiltinNode()
        : this(Guid.Empty, string.Empty, BuiltinNodeType.Copy, 1, [], [], []) { }
}
