using ProtoBuf;

namespace SonicEddy.Contracts.Mixers;

[ProtoContract]
public record Mixer(
    [property: ProtoMember(1)] Guid Id,
    [property: ProtoMember(2)] string Name,
    [property: ProtoMember(3)] List<ChannelStrip> ChannelStrips)
{
    public Mixer() : this(Guid.Empty, string.Empty, [])
    {
    }
}