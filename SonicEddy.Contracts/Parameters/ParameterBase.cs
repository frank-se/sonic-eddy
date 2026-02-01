using ProtoBuf;

namespace SonicEddy.Contracts.Parameters;

[ProtoContract]
[ProtoInclude(100, typeof(FloatParameter))]
[ProtoInclude(101, typeof(StringParameter))]
[ProtoInclude(102, typeof(LongParameter))]
[ProtoInclude(103, typeof(IntParameter))]
[ProtoInclude(104, typeof(DoubleParameter))]
[ProtoInclude(105, typeof(BoolParameter))]
public record ParameterBase(
    [property: ProtoMember(1)] string Name);