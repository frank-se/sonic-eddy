using ProtoBuf;

namespace SonicEddy.Contracts.ExternalEffects;

[ProtoContract]
public sealed class ExternalEffectsConfig
{
    [ProtoMember(1)]
    public List<ExternalEffectConfig> Effects { get; set; } = [];
}

[ProtoContract]
public sealed class ExternalEffectConfig
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public string Name { get; set; } = string.Empty;
    [ProtoMember(3)] public string InputNodeName { get; set; } = string.Empty;
    [ProtoMember(4)] public List<ExternalEffectPortConfig> InputPorts { get; set; } = [];
    [ProtoMember(5)] public string OutputNodeName { get; set; } = string.Empty;
    [ProtoMember(6)] public List<ExternalEffectPortConfig> OutputPorts { get; set; } = [];
}

[ProtoContract]
public sealed class ExternalEffectPortConfig
{
    [ProtoMember(1)] public string Name { get; set; } = string.Empty;
    [ProtoMember(2)] public string Alias { get; set; } = string.Empty;
    [ProtoMember(3)] public string Channel { get; set; } = string.Empty;
}
