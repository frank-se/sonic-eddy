using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
[ProtoInclude(100, typeof(FilterGraphInput))]
[ProtoInclude(101, typeof(FilterGraphLv2Plugin))]
[ProtoInclude(102, typeof(FilterGraphOutput))]
public abstract record FilterGraphNodeBase(
    [property: ProtoMember(1)] Guid Id,
    [property: ProtoMember(2)] string Name)
{
}