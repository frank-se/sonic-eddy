using ProtoBuf;

namespace SonicEddy.Contracts.ApplicationPreferences;

[ProtoContract]
public record Preferences(
    [property: ProtoMember(1)] string? DefaultMasterOutputName,
    [property: ProtoMember(2)] int NumberOfChannels,
    [property: ProtoMember(3)] int NumberOfGroupChannels,
    [property: ProtoMember(4)] int NumberOfReturnChannels,
    [property: ProtoMember(5)] string? DefaultMonitorOutputName,
    [property: ProtoMember(6)] bool MonitoringChannelEnabled,
    [property: ProtoMember(7)] string? TraktorZ1HidrawPath,
    [property: ProtoMember(8)] string? DefaultCueOutputName,
    [property: ProtoMember(9)] bool DrumMixerEnabled,
    [property: ProtoMember(10)] bool MicChannelEnabled)
{
    public Preferences() : this(null, 8, 4, 1, null, false, "/dev/hidraw3",
        null, false, false)
    {
    }
}
