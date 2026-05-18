using System.Runtime.InteropServices;
using Fr.Sonic.PInvoke;

namespace Fr.Sonic;

public record LayerSelectEventArgs(ulong LayerId);
public record ChannelSelectEventArgs(ChannelType ChannelType, ulong ChannelId);
public record DialModeEventArgs(ChannelType ChannelType, ulong ChannelId, DialMode DialMode);
public record FilterSectionEventArgs(ChannelType ChannelType, ulong ChannelId, ulong SectionId);

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

    /* ── internal callbacks forwarded to MidiPortFactory ──────────────── */

    internal static void OnLayerSelect(ulong layerId) =>
        LayerChanged?.Invoke(null, new LayerSelectEventArgs(layerId));

    internal static void OnChannelSelect(ChannelType channelType, ulong channelId) =>
        ChannelChanged?.Invoke(null, new ChannelSelectEventArgs(channelType, channelId));

    internal static void OnDialSectionModeSelect(ChannelType channelType,
        ulong channelId, DialMode dialMode) =>
        DialModeChanged?.Invoke(null,
            new DialModeEventArgs(channelType, channelId, dialMode));

    internal static void OnFilterParamsSectionSelect(ChannelType channelType,
        ulong channelId, ulong sectionId) =>
        FilterSectionChanged?.Invoke(null,
            new FilterSectionEventArgs(channelType, channelId, sectionId));

    internal static void OnFilterParamsSectionMoveRight(ulong stepCount) =>
        FilterSectionMovedRight?.Invoke(null, stepCount);

    internal static void OnFilterParamsSectionMoveLeft(ulong stepCount) =>
        FilterSectionMovedLeft?.Invoke(null, stepCount);

    /* ── events ───────────────────────────────────────────────────────── */

    public static event EventHandler<LayerSelectEventArgs>? LayerChanged;
    public static event EventHandler<ChannelSelectEventArgs>? ChannelChanged;
    public static event EventHandler<DialModeEventArgs>? DialModeChanged;
    public static event EventHandler<FilterSectionEventArgs>? FilterSectionChanged;
    public static event EventHandler<ulong>? FilterSectionMovedRight;
    public static event EventHandler<ulong>? FilterSectionMovedLeft;

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
