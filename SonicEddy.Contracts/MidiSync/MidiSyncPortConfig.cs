using ProtoBuf;

namespace SonicEddy.Contracts.MidiSync;

[ProtoContract]
public class MidiSyncPortConfig
{
    [ProtoMember(1)]
    public string NodeName { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string PortName { get; set; } = string.Empty;
}
