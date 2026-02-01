using ProtoBuf;

namespace SonicEddy.Contracts.Parameters;

[ProtoContract]
public record BoolParameter(
    string Name,
    [property: ProtoMember(1)] bool Value) : ParameterBase(Name)
{
    public BoolParameter() : this(string.Empty, false)
    {
    }
}