using ProtoBuf;

namespace SonicEddy.Contracts.Parameters;

[ProtoContract]
public record StringParameter(
    string Name,
    [property: ProtoMember(1)] string Value) : ParameterBase(Name)
{
    public StringParameter() : this(string.Empty, string.Empty)
    {
    }
}