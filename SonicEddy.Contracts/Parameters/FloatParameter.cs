using ProtoBuf;

namespace SonicEddy.Contracts.Parameters;

[ProtoContract]
public record FloatParameter(
    string Name,
    [property: ProtoMember(1)] float Value)
    : ParameterBase(Name)
{
    public FloatParameter() : this(string.Empty, 0.0f)
    {
    }
}