using System.Runtime.InteropServices;
using Fr.Sonic.PInvoke;

namespace Fr.Sonic;

public record MidiCcUpdate(
    ChannelType ChannelType, ulong ChannelId, ulong ObjectId,
    string ParameterName, float NormalizedValue, float NormalizedKnownValue,
    bool CatchingUp);

/// <summary>
/// MIDI controller mapping facade.
/// </summary>
public static class FrSonicMidi
{
    public static event EventHandler<MidiCcUpdate>? ControlChangeUpdate;

    internal static void OnMidiCcUpdate(ChannelType channelType, ulong channelId,
        ulong objectId, IntPtr parameterName, float normalizedValue,
        float normalizedKnownValue, bool catchingUp)
    {
        var update = new MidiCcUpdate(channelType, channelId, objectId,
            Marshal.PtrToStringUTF8(parameterName) ?? string.Empty,
            normalizedValue, normalizedKnownValue, catchingUp);
        ControlChangeUpdate?.Invoke(null, update);
    }

    public const string PmxPurpose = "midi-controller";

    public static ulong CreateMidiMixPort(string pmxTag,
        LayerSelectCallback layerCb, ChannelSelectCallback channelCb,
        DialModeCallback dialModeCb, FilterSectionCallback filterSectionCb) =>
        FrSonicLib.CreateMidiMixPortC(PmxPurpose, pmxTag, layerCb, channelCb,
            dialModeCb, filterSectionCb);

    public static ulong CreateMm1Port(string pmxTag,
        LayerSelectCallback layerCb, ChannelSelectCallback channelCb,
        DialModeCallback dialModeCb, FilterSectionCallback filterSectionCb,
        PagesRightCallback pagesRightCb, PagesLeftCallback pagesLeftCb) =>
        FrSonicLib.CreateMm1PortC(PmxPurpose, pmxTag, layerCb, channelCb,
            dialModeCb, filterSectionCb, pagesRightCb, pagesLeftCb);

    public static ulong CreateFaderFoxPc4Port(string pmxTag) =>
        FrSonicLib.CreateFaderFoxPc4PortC(PmxPurpose, pmxTag);

    public static void SetSelectedPluginPage(ulong pluginId, ulong pageNumber) =>
        FrSonicLib.SetSelectedPluginPageC(pluginId, pageNumber);

    public static void SetSelectedChannel(ChannelType channelType, ulong channelId) =>
        FrSonicLib.SetSelectedChannelC(channelType, channelId);

    public static void ClearSelectedChannel() =>
        FrSonicLib.ClearSelectedChannelC();

    public static void SetSelectedLayer(ulong layerId) =>
        FrSonicLib.SetSelectedLayerC(layerId);

    public static void SetChannelNode(ChannelType channelType, ulong channelId,
        ulong objectId) =>
        FrSonicLib.SetChannelNodeC(channelType, channelId, objectId);

    public static void SetMasterChannelNode(ulong layerId, ulong objectId) =>
        FrSonicLib.SetMasterChannelNodeC(layerId, objectId);

    public static void SetChannelFilterNode(ChannelType channelType, ulong channelId,
        ulong objectId) =>
        FrSonicLib.SetChannelFilterNodeC(channelType, channelId, objectId);

    public static void SetChannelSendNode(ChannelType channelType, ulong channelId,
        ulong sendId, ulong objectId) =>
        FrSonicLib.SetChannelSendNodeC(channelType, channelId, sendId, objectId);

    public static void ClearFilterParameters(ChannelType channelType,
        ulong channelId) =>
        FrSonicLib.ClearFilterParametersC(channelType, channelId);

    public static void AddFilterParameter(ChannelType channelType, ulong channelId,
        ulong pluginId, string name, float min, float max) =>
        FrSonicLib.AddFilterParameterC(channelType, channelId, pluginId,
            name, min, max);
}
