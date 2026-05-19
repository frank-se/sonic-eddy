using Fr.Sonic;
using Fr.Sonic.PInvoke;

namespace SonicEddy.Services.Midi;

public class MidiControllerSetupService : IMidiControllerSetupService
{
    public void SetChannelNode(ChannelType channelType, ulong channelId,
        ulong objectId) =>
        FrSonicMidi.SetChannelNode(channelType, channelId, objectId);

    public void SetMasterChannelNode(ulong layerId, ulong objectId) =>
        FrSonicMidi.SetMasterChannelNode(layerId, objectId);

    public void SetChannelFilterNode(ChannelType channelType, ulong channelId,
        ulong objectId) =>
        FrSonicMidi.SetChannelFilterNode(channelType, channelId, objectId);

    public void SetChannelSendNode(ChannelType channelType, ulong channelId,
        ulong sendId,
        ulong objectId) =>
        FrSonicMidi.SetChannelSendNode(channelType, channelId, sendId, objectId);

    public void ClearFilterParameters(ChannelType channelType, ulong channelId)
        => FrSonicMidi.ClearFilterParameters(channelType, channelId);

    public void AddFilterParameter(ChannelType channelType, ulong channelId,
        ulong pluginId, string name, float min, float max) =>
        FrSonicMidi.AddFilterParameter(channelType, channelId, pluginId, name, min,
            max);
}