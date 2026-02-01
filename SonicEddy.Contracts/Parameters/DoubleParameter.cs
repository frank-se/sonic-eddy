using ProtoBuf;

namespace SonicEddy.Contracts.Parameters;

[ProtoContract]
public record DoubleParameter(
    string Name,
    [property: ProtoMember(1)] double Value) : ParameterBase(Name)
{
    public DoubleParameter() : this(string.Empty, 0.0)
    {
    }
}