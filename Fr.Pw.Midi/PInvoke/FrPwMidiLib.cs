using System.Runtime.InteropServices;

namespace Fr.Pw.Midi.PInvoke;

// ReSharper disable InconsistentNaming
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LayerSelectCallback(ulong layer_id);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void ChannelSelectCallback(
    ChannelType channel_type,
    ulong channel_id);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void DialSectionModeSelectCallback(
    ChannelType channel_type,
    ulong channel_id, DialMode dial_mode);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void FilterParamsSectionSelectCallback(
    ChannelType channel_type,
    ulong channel_id, ulong section_id);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void FilterParamsSectionMovePagesRightCallback(
    ulong step_count);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void FilterParamsSectionMovePagesLeftCallback(
    ulong step_count);

internal static partial class FrPwMidiLib
{
    private const string LIBRARY_NAME = "libfrmidimapper.so.0.0.4";

    [LibraryImport(LIBRARY_NAME, EntryPoint = "init")]
    private static partial void InitC();

    [LibraryImport(LIBRARY_NAME, EntryPoint = "start")]
    private static partial void StartC();

    [LibraryImport(LIBRARY_NAME, EntryPoint = "stop")]
    private static partial void StopC();

    [LibraryImport(LIBRARY_NAME, EntryPoint = "create_midi_mix_port",
        StringMarshalling = StringMarshalling.Utf8)]
    private static partial ulong CreateMidiMixPortC(
        string pmx_purpose,
        string pmx_tag,
        LayerSelectCallback layer_select_callback,
        ChannelSelectCallback channel_select_callback,
        DialSectionModeSelectCallback dial_section_mode_select_callback,
        FilterParamsSectionSelectCallback filter_params_callback);

    [LibraryImport(LIBRARY_NAME, EntryPoint = "create_mm_1_port",
        StringMarshalling = StringMarshalling.Utf8)]
    private static partial ulong CreateMm1PortC(
        string pmx_purpose,
        string pmx_tag,
        LayerSelectCallback layer_select_callback,
        ChannelSelectCallback channel_select_callback,
        DialSectionModeSelectCallback dial_section_mode_callback,
        FilterParamsSectionSelectCallback filter_params_callback,
        FilterParamsSectionMovePagesRightCallback move_right_callback,
        FilterParamsSectionMovePagesLeftCallback move_left_callback);

    [LibraryImport(LIBRARY_NAME, EntryPoint = "create_fader_fox_pc4_port",
        StringMarshalling = StringMarshalling.Utf8)]
    private static partial ulong CreateFaderFoxPc4PortC(string pmx_purpose,
        string pmx_tag);

    [LibraryImport(LIBRARY_NAME, EntryPoint = "set_selected_plugin_page")]
    private static partial void SetSelectedPluginPageC(ulong plugin_id,
        ulong page_number);

    [LibraryImport(LIBRARY_NAME, EntryPoint = "set_selected_channel")]
    private static partial void SetSelectedChannelC(ChannelType channel_type,
        ulong channel_id);

    [LibraryImport(LIBRARY_NAME, EntryPoint = "clear_selected_channel")]
    private static partial void ClearSelectedChannelC();
    
    [LibraryImport(LIBRARY_NAME, EntryPoint = "set_selected_layer")]
    private static partial void SetSelectedLayerC(ulong layer_id);

    [LibraryImport(LIBRARY_NAME, EntryPoint = "set_channel_node")]
    private static partial void SetChannelNodeC(ChannelType channel_type,
        ulong channel_id, ulong object_id);

    [LibraryImport(LIBRARY_NAME, EntryPoint = "set_master_channel_node")]
    private static partial void SetMasterChannelNodeC(ulong layer_id,
        ulong object_id);

    [LibraryImport(LIBRARY_NAME, EntryPoint = "set_channel_filter_node")]
    private static partial void SetChannelFilterNodeC(ChannelType channel_type,
        ulong channel_id, ulong object_id);

    [LibraryImport(LIBRARY_NAME, EntryPoint = "set_channel_send_node")]
    private static partial void SetChannelSendNodeC(ChannelType channel_type,
        ulong channel_id, ulong send_id, ulong object_id);

    [LibraryImport(LIBRARY_NAME, EntryPoint = "clear_filter_parameters")]
    private static partial void ClearFilterParametersC(ChannelType channel_type,
        ulong channel_id);

    [LibraryImport(LIBRARY_NAME, EntryPoint = "add_filter_parameter",
        StringMarshalling = StringMarshalling.Utf8)]
    private static partial void AddFilterParameterC(ChannelType channel_type,
        ulong channel_id,
        ulong plugin_id, string name, float min, float max);

    internal static void Init() => InitC();
    internal static void Start() => StartC();
    internal static void Stop() => StopC();

    internal const string PMX_PURPOSE = "midi-controller";

    internal static ulong CreateMidiMixPort(
        string pmxTag,
        LayerSelectCallback layerSelectCallback,
        ChannelSelectCallback channelSelectCallback,
        DialSectionModeSelectCallback dialSectionModeSelectCallback,
        FilterParamsSectionSelectCallback filterParamsCallback) =>
        CreateMidiMixPortC(PMX_PURPOSE, pmxTag,
            layerSelectCallback,
            channelSelectCallback, dialSectionModeSelectCallback,
            filterParamsCallback);

    internal static ulong CreateCmdMm1Port(string pmxTag,
        LayerSelectCallback layerSelectCallback,
        ChannelSelectCallback channelSelectCallback,
        DialSectionModeSelectCallback dialSectionModeSelectCallback,
        FilterParamsSectionSelectCallback filterParamsSectionSelectCallback,
        FilterParamsSectionMovePagesRightCallback
            filterParamsSectionMovePagesRightCallback,
        FilterParamsSectionMovePagesLeftCallback
            filterParamsSectionMovePagesLeftCallback) => CreateMm1PortC(
        PMX_PURPOSE, pmxTag, layerSelectCallback, channelSelectCallback,
        dialSectionModeSelectCallback, filterParamsSectionSelectCallback,
        filterParamsSectionMovePagesRightCallback,
        filterParamsSectionMovePagesLeftCallback);

    internal static ulong CreateFaderFoxPc4Port(string pmxTag) =>
        CreateFaderFoxPc4PortC(PMX_PURPOSE, pmxTag);

    internal static void
        SetSelectedPluginPage(ulong pluginId, ulong pageNumber) =>
        SetSelectedPluginPageC(pluginId, pageNumber);

    internal static void SetSelectedChannel(ChannelType channelType,
        ulong channelId) => SetSelectedChannelC(channelType, channelId);

    internal static void ClearSelectedChannel() => ClearSelectedChannelC();
    
    internal static void SetSelectedLayer(ulong layerId) =>
        SetSelectedLayerC(layerId);

    internal static void SetChannelNode(ChannelType channelType,
        ulong channelId, ulong objectId) =>
        SetChannelNodeC(channelType, channelId, objectId);

    internal static void SetMasterChannelNode(ulong layer_id,
        ulong object_id) => SetMasterChannelNodeC(layer_id,
        object_id);

    internal static void SetChannelFilterNode(ChannelType channelType,
        ulong channelId, ulong objectId) =>
        SetChannelFilterNodeC(channelType, channelId, objectId);

    internal static void SetChannelSendNode(ChannelType channelType,
        ulong channelId, ulong sendId, ulong objectId) =>
        SetChannelSendNodeC(channelType, channelId, sendId, objectId);

    internal static void ClearFilterParameters(ChannelType channelType,
        ulong channelId) => ClearFilterParametersC(channelType, channelId);

    internal static void AddFilterParameter(ChannelType channelType,
        ulong channelId, ulong pluginId, string name, float min, float max) =>
        AddFilterParameterC(channelType, channelId, pluginId, name, min, max);
}