using ProtoBuf;

namespace SonicEddy.Contracts.Parameters;

[ProtoContract]
public record IntParameter(
    string Name,
    [property: ProtoMember(1)] int Value) : ParameterBase(Name)
{
    public IntParameter() : this(string.Empty, 0)
    {
    }
}