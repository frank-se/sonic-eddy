using Fr.Pw.Midi;
using Fr.Pw.Midi.PInvoke;

namespace SonicEddy.Services.Midi;

public class MidiControllerSetupService : IMidiControllerSetupService
{
    public void SetChannelNode(ChannelType channelType, ulong channelId,
        ulong objectId) =>
        FrPwMidi.SetChannelNode(channelType, channelId, objectId);

    public void SetMasterChannelNode(ulong layerId, ulong objectId) =>
        FrPwMidi.SetMasterChannelNode(layerId, objectId);

    public void SetChannelFilterNode(ChannelType channelType, ulong channelId,
        ulong objectId) =>
        FrPwMidi.SetChannelFilterNode(channelType, channelId, objectId);

    public void SetChannelSendNode(ChannelType channelType, ulong channelId,
        ulong sendId,
        ulong objectId) =>
        FrPwMidi.SetChannelSendNode(channelType, channelId, sendId, objectId);

    public void ClearFilterParameters(ChannelType channelType, ulong channelId)
        => FrPwMidi.ClearFilterParameters(channelType, channelId);

    public void AddFilterParameter(ChannelType channelType, ulong channelId,
        ulong pluginId, string name, float min, float max) =>
        FrPwMidi.AddFilterParameter(channelType, channelId, pluginId, name, min,
            max);
}