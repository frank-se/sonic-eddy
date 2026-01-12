using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph.Builtins;

[ProtoContract]
public record FilterGraphMixerPlugin(
    Guid Id,
    string Name,
    [property: ProtoMember(1)] List<FilterGraphInputPort> InputPorts,
    [property: ProtoMember(2)] List<FilterGraphOutputPort> OutputPorts)
    : FilterGraphNodeBase(Id, Name)
{
    public FilterGraphMixerPlugin() : this(Guid.Empty, string.Empty, [], [])
    {
    }

    public FilterGraphMixerPlugin(Guid Id, string Name) : this(
        Id,
        Name,
        [
            new(Guid.NewGuid(), "In 1"),
            new(Guid.NewGuid(), "In 2"),
            new(Guid.NewGuid(), "In 3"),
            new(Guid.NewGuid(), "In 4"),
            new(Guid.NewGuid(), "In 5"),
            new(Guid.NewGuid(), "In 6"),
            new(Guid.NewGuid(), "In 7"),
            new(Guid.NewGuid(), "In 8")
        ],
        [new(Guid.NewGuid(), "Out")])
    {
    }
}