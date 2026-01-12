using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
[ProtoInclude(100, typeof(FilterGraphOutputPort))]
[ProtoInclude(101, typeof(FilterGraphInputPort))]
public abstract record FilterGraphPortBase([property: ProtoMember(1)] Guid Id);