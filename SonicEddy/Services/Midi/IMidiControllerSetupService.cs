using Fr.Sonic.PInvoke;

namespace SonicEddy.Services.Midi;

public interface IMidiControllerSetupService
{
    void SetChannelNode(ChannelType channelType,
        ulong channelId, ulong objectId);

    void SetMasterChannelNode(ulong layerId, ulong objectId);

    void SetChannelFilterNode(ChannelType channelType,
        ulong channelId, ulong objectId);

    void SetChannelSendNode(ChannelType channelType,
        ulong channelId, ulong sendId, ulong objectId);

    void ClearFilterParameters(ChannelType channelType,
        ulong channelId);

    void AddFilterParameter(ChannelType channelType,
        ulong channelId, ulong pluginId, string name, float min, float max);
}