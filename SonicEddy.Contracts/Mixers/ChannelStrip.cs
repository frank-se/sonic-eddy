using ProtoBuf;
using SonicEddy.Contracts.Parameters;

namespace SonicEddy.Contracts.Mixers;

[ProtoContract]
public record ChannelStrip(
    [property: ProtoMember(1)] ulong ChannelId,
    [property: ProtoMember(2)] string Name,
    [property: ProtoMember(3)] string InputNodeName,
    [property: ProtoMember(4)] List<float> ChannelVolumes,
    [property: ProtoMember(5)] Guid? FilterGraphId,
    [property: ProtoMember(6)] List<ParameterBase>? Parameters)
{
    public ChannelStrip() : this(0u, string.Empty, string.Empty, [], null, [])
    {
    }
}