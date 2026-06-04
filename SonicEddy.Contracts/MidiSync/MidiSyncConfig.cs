using ProtoBuf;

namespace SonicEddy.Contracts.MidiSync;

[ProtoContract]
public class MidiSyncConfig
{
    [ProtoMember(1)]
    public List<MidiSyncPortConfig> Ports { get; set; } = [];
}
