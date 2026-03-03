using ProtoBuf;

namespace SonicEddy.Contracts.ApplicationPreferences;

[ProtoContract]
public record Preferences(
    [property: ProtoMember(1)] string? DefaultMasterOutputName)
{
    public Preferences() : this((string?)null)
    {
    }
}