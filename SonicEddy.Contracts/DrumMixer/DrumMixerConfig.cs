using ProtoBuf;

namespace SonicEddy.Contracts.DrumMixer;

[ProtoContract]
public sealed class DrumMixerConfig
{
    [ProtoMember(1)] public List<DrumMixerChannelConfig> Channels { get; set; } = [];
    [ProtoMember(2)] public DrumMixerReturnConfig Room { get; set; } = new();
    [ProtoMember(3)] public DrumMixerReturnConfig Plate { get; set; } = new();
}

[ProtoContract]
public sealed class DrumMixerChannelConfig
{
    [ProtoMember(1)] public string Name { get; set; } = string.Empty;
    [ProtoMember(2)] public string? SourceNodeName { get; set; }
    [ProtoMember(3)] public string? SourcePortName { get; set; }
    [ProtoMember(4)] public float Volume { get; set; } = 1.0f;
    [ProtoMember(5)] public float RoomSend { get; set; }
    [ProtoMember(6)] public float PlateSend { get; set; }
    [ProtoMember(7)] public List<DrumMixerParameterValue> Parameters { get; set; } = [];
}

[ProtoContract]
public sealed class DrumMixerReturnConfig
{
    [ProtoMember(1)] public float Volume { get; set; } = 1.0f;
    [ProtoMember(2)] public List<DrumMixerParameterValue> Parameters { get; set; } = [];
}

[ProtoContract]
public sealed class DrumMixerParameterValue
{
    [ProtoMember(1)] public string Name { get; set; } = string.Empty;
    [ProtoMember(2)] public float Value { get; set; }
}
