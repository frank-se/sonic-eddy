using System.Runtime.InteropServices;
using Fr.Sonic.PInvoke;

namespace Fr.Sonic;

/* ── event arg types (matching Fr.Pw.Midi.FrPwMidi API surface) ─────────── */

public record LayerSelectEventArgs(ulong LayerId);

public record ChannelSelectEventArgs(ChannelType ChannelType, ulong ChannelId);

public record DialSelectionModeSelectEventArgs(
    ChannelType ChannelType, ulong ChannelId, DialMode DialMode);

public record FilterParamsSectionSelectEventArgs(
    ChannelType ChannelType, ulong ChannelId, ulong SectionId);

public record FilterParamsSectionMovePagesEventArgs(ulong StepCount);

public record MidiControlChangeUpdateEventArgs(
    ChannelType ChannelType,
    ulong ChannelId,
    ulong ObjectId,
    string ParameterName,
    float NormalizedControllerValue,
    float NormalizedKnownValue,
    bool CatchingUp);

public record LaunchpadLoopPressedEventArgs(
    ulong LayerId,
    ChannelType ChannelType,
    ulong ChannelId,
    ulong LoopPosition);

public record LaunchpadFaderChangedEventArgs(
    ulong LayerId,
    ChannelType ChannelType,
    ulong ChannelId,
    float NormalizedValue);

/// <summary>
/// MIDI controller mapping facade.
/// </summary>
public static class FrSonicMidi
{
    /* ── events (Action<T>, matching Fr.Pw.Midi.FrPwMidi names) ─────────── */

    public static event Action<LayerSelectEventArgs>? LayerChanged;
    public static event Action<ChannelSelectEventArgs>? SelectedChannelChanged;
    public static event Action<DialSelectionModeSelectEventArgs>? DialSelectionModeChanged;
    public static event Action<FilterParamsSectionSelectEventArgs>? SelectedFilterParamsSectionChanged;
    public static event Action<FilterParamsSectionMovePagesEventArgs>? FilterParamsSectionMovedRight;
    public static event Action<FilterParamsSectionMovePagesEventArgs>? FilterParamsSectionMovedLeft;
    public static event Action<MidiControlChangeUpdateEventArgs>? MidiControlChangeUpdate;
    public static event Action<LaunchpadLoopPressedEventArgs>? LaunchpadLoopPressed;
    public static event Action<LaunchpadFaderChangedEventArgs>? LaunchpadFaderChanged;

    /* ── internal callbacks ──────────────────────────────────────────────── */

    internal static void OnMidiCcUpdate(ChannelType channelType, ulong channelId,
        ulong objectId, IntPtr parameterName, float normalizedValue,
        float normalizedKnownValue, bool catchingUp) =>
        MidiControlChangeUpdate?.Invoke(new(channelType, channelId, objectId,
            Marshal.PtrToStringUTF8(parameterName) ?? string.Empty,
            normalizedValue, normalizedKnownValue, catchingUp));

    internal static void OnLaunchpadLoopPressed(ulong layerId,
        ChannelType channelType, ulong channelId, ulong loopPosition) =>
        LaunchpadLoopPressed?.Invoke(new(layerId, channelType, channelId,
            loopPosition));

    internal static void OnLaunchpadFaderChanged(ulong layerId,
        ChannelType channelType, ulong channelId, float normalizedValue) =>
        LaunchpadFaderChanged?.Invoke(new(layerId, channelType, channelId,
            normalizedValue));

    internal static void OnLayerSelect(ulong layerId) =>
        LayerChanged?.Invoke(new(layerId));

    internal static void OnChannelSelect(ChannelType channelType, ulong channelId) =>
        SelectedChannelChanged?.Invoke(new(channelType, channelId));

    internal static void OnDialSectionModeSelect(ChannelType channelType,
        ulong channelId, DialMode dialMode) =>
        DialSelectionModeChanged?.Invoke(new(channelType, channelId, dialMode));

    internal static void OnFilterParamsSectionSelect(ChannelType channelType,
        ulong channelId, ulong sectionId) =>
        SelectedFilterParamsSectionChanged?.Invoke(
            new(channelType, channelId, sectionId));

    internal static void OnFilterParamsSectionMoveRight(ulong stepCount) =>
        FilterParamsSectionMovedRight?.Invoke(new(stepCount));

    internal static void OnFilterParamsSectionMoveLeft(ulong stepCount) =>
        FilterParamsSectionMovedLeft?.Invoke(new(stepCount));

    /* ── public API ──────────────────────────────────────────────────────── */

    public const string PmxPurpose = "midi-controller";

    public static void SetSelectedPluginPage(ulong pluginId, ulong pageNumber) =>
        FrSonicLib.SetSelectedPluginPageC(pluginId, pageNumber);

    public static void SetSelectedChannel(ChannelType channelType, ulong channelId) =>
        FrSonicLib.SetSelectedChannelC(channelType, channelId);

    public static void ClearSelectedChannel() => FrSonicLib.ClearSelectedChannelC();

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
        FrSonicLib.AddFilterParameterC(channelType, channelId, pluginId, name, min, max);

    public static void SetLaunchpadMiniLooperSlotState(ChannelType channelType,
        ulong channelId, ulong loopPosition, bool available, bool loaded,
        bool playing) =>
        FrSonicLib.SetLaunchpadMiniLooperSlotStateC(channelType, channelId,
            loopPosition, available, loaded, playing);
}
