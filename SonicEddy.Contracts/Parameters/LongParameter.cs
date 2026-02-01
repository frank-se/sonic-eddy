using ProtoBuf;

namespace SonicEddy.Contracts.Parameters;

[ProtoContract]
public record LongParameter(
    string Name,
    [property: ProtoMember(1)] long Value) : ParameterBase(Name)
{
    public LongParameter() : this(string.Empty, 0)
    {
    }
}