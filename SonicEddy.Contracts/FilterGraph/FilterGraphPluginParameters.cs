using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public record FilterGraphPluginParameters(
    [property: ProtoMember(1)] Guid NodeId,
    [property: ProtoMember(2)]
    List<FilterGraphParameterDescription> ParameterDescriptions)
{
    public FilterGraphPluginParameters() : this(Guid.Empty, [])
    {
    }
};