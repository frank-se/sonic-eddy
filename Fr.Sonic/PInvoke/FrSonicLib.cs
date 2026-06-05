using System.Runtime.InteropServices;
using Fr.Sonic.Model;

// ReSharper disable InconsistentNaming

namespace Fr.Sonic.PInvoke;

/* ── enums ───────────────────────────────────────────────────────────────── */

public enum ChannelType
{
    Channel = 0,
    GroupChannel = 1,
    Master = 2,
}

public enum DialMode
{
    Sends = 1,
    FilterParams = 2,
}

/* WireplumberObjectType removed — wireplumber_object_type from WireplumberData.cs is used instead */

/* ── callback delegates ──────────────────────────────────────────────────── */

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void NodeAddedCallback(IntPtr node);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void PropsChangedCallback(IntPtr propsPtr, IntPtr paramUpdatePtr);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void PropsEnumFailedCallback(ulong objectSerial);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void PropInfoAddedCallback(IntPtr jsonPtr);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void ObjectDeletedCallback(ulong objectSerial, wireplumber_object_type objectType);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void MetadataAddedCallback(IntPtr metadataName);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void MetadataEntryUpdatedCallback(
    IntPtr metadataName,
    ulong subject,
    IntPtr key,
    IntPtr type,
    IntPtr value
);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void MetadataEntryDeletedCallback(IntPtr metadataName, ulong subject, IntPtr key);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void PeakCallback(
    ulong objectSerial,
    float leftPeak,
    float rightPeak,
    float leftAverage,
    float rightAverage
);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void LayerSelectCallback(ulong layerId);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void ChannelSelectCallback(ChannelType channelType, ulong channelId);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void DialModeCallback(ChannelType channelType, ulong channelId, DialMode dialMode);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void FilterSectionCallback(
    ChannelType channelType,
    ulong channelId,
    ulong sectionId
);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void PagesRightCallback(ulong stepCount);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void PagesLeftCallback(ulong stepCount);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void MidiCcUpdateCallback(
    ChannelType channelType,
    ulong channelId,
    ulong objectId,
    IntPtr parameterName,
    float normalizedValue,
    float normalizedKnownValue,
    [MarshalAs(UnmanagedType.U1)] bool catchingUp
);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void LaunchpadLoopPressedCallback(
    ulong layerId,
    ChannelType channelType,
    ulong channelId,
    ulong loopPosition
);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void LaunchpadFaderChangedCallback(
    ulong layerId,
    ChannelType channelType,
    ulong channelId,
    float normalizedValue
);

/* ── structs ─────────────────────────────────────────────────────────────── */

[StructLayout(LayoutKind.Sequential)]
internal struct FrSonicLooperConfig
{
    internal IntPtr Name;
    internal IntPtr Tag;
    internal IntPtr Description;
    internal IntPtr CaptureTargetObject;
    internal IntPtr PlaybackTargetObject;
    internal IntPtr ArchiveFolderPath;
    internal uint Channels;
    internal uint MaxRecordSeconds;
    internal float Mix;
    internal IntPtr PlaybackAudioPosition;
}

[StructLayout(LayoutKind.Sequential)]
internal struct FrSonicClickSyncConfig
{
    internal IntPtr Id;
    internal IntPtr Name;
    internal IntPtr Tag;
    internal uint PulsesPerQuarterNote;
    internal double PulseLengthMs;
    internal float PulseAmplitude;
}

/* ── P/Invoke declarations ───────────────────────────────────────────────── */

internal static partial class FrSonicLib
{
    private const string LIB = "libfrsonic.so.0.5.0";

    /* lifecycle */
    [LibraryImport(LIB, EntryPoint = "frsonic_init")]
    internal static partial void InitC(
        NodeAddedCallback nodeAdded,
        PropsChangedCallback propsChanged,
        PropsEnumFailedCallback propsEnumFailed,
        PropInfoAddedCallback propInfoAdded,
        ObjectDeletedCallback objectDeleted,
        MetadataAddedCallback metadataAdded,
        MetadataEntryUpdatedCallback metadataEntryUpdated,
        MetadataEntryDeletedCallback metadataEntryDeleted,
        PeakCallback peak,
        ulong peakUpdateIntervalMs,
        MidiCcUpdateCallback midiCcUpdate
    );

    [LibraryImport(LIB, EntryPoint = "frsonic_start")]
    internal static partial void StartC();

    [LibraryImport(LIB, EntryPoint = "frsonic_stop")]
    internal static partial void StopC();

    /* loopers */
    [LibraryImport(LIB, EntryPoint = "frsonic_create_looper")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool CreateLooperC(in FrSonicLooperConfig config, out UIntPtr handle);

    [LibraryImport(LIB, EntryPoint = "frsonic_destroy_looper")]
    internal static partial void DestroyLooperC(UIntPtr handle);

    [LibraryImport(LIB, EntryPoint = "frsonic_create_midi_manipulator")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool CreateMidiManipulatorC(
        in FrSonicMidiManipulatorConfig config,
        out UIntPtr handle
    );

    [LibraryImport(LIB, EntryPoint = "frsonic_destroy_midi_manipulator")]
    internal static partial void DestroyMidiManipulatorC(UIntPtr handle);

    [LibraryImport(LIB, EntryPoint = "frsonic_create_click_sync_converter")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool CreateClickSyncConverterC(
        in FrSonicClickSyncConfig config,
        out UIntPtr handle
    );

    [LibraryImport(LIB, EntryPoint = "frsonic_destroy_click_sync_converter")]
    internal static partial void DestroyClickSyncConverterC(UIntPtr handle);

    /* wireplumber object model */
    [LibraryImport(LIB, EntryPoint = "frsonic_set_volumes")]
    internal static partial void SetVolumesC(
        ulong objectId,
        ReadOnlySpan<double> volumes,
        nuint count
    );

    [LibraryImport(LIB, EntryPoint = "frsonic_set_mute")]
    internal static partial void SetMuteC(ulong objectId, [MarshalAs(UnmanagedType.U1)] bool mute);

    [LibraryImport(LIB, EntryPoint = "frsonic_set_params")]
    internal static partial void SetParamsC(
        ulong objectId,
        IntPtr keys,
        ReadOnlySpan<ParamType> paramTypes,
        ReadOnlySpan<ulong> values,
        nuint count
    );

    [LibraryImport(
        LIB,
        EntryPoint = "frsonic_load_module",
        StringMarshalling = StringMarshalling.Utf8
    )]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool LoadModuleC(string name, string config, out IntPtr handle);

    [LibraryImport(LIB, EntryPoint = "frsonic_destroy_module")]
    internal static partial void DestroyModuleC(IntPtr handle);

    [LibraryImport(
        LIB,
        EntryPoint = "frsonic_set_metadata_entry",
        StringMarshalling = StringMarshalling.Utf8
    )]
    internal static partial void SetMetadataEntryC(
        string metadataName,
        ulong subject,
        string key,
        string type,
        string value
    );

    [LibraryImport(
        LIB,
        EntryPoint = "frsonic_delete_metadata_entry",
        StringMarshalling = StringMarshalling.Utf8
    )]
    internal static partial void DeleteMetadataEntryC(
        string metadataName,
        ulong subject,
        string key
    );

    [LibraryImport(LIB, EntryPoint = "frsonic_create_link_by_port_ids")]
    internal static partial void CreateLinkByPortIdsC(
        ulong outPortId,
        ulong inPortId,
        [MarshalAs(UnmanagedType.U1)] bool linger
    );

    [LibraryImport(LIB, EntryPoint = "frsonic_delete_link_by_object_id")]
    internal static partial void DeleteLinkByObjectIdC(ulong objectId);

    /* monitoring */
    [LibraryImport(LIB, EntryPoint = "frsonic_start_monitor_node")]
    internal static partial void StartMonitorNodeC(ulong objectSerial);

    [LibraryImport(LIB, EntryPoint = "frsonic_stop_monitor_node")]
    internal static partial void StopMonitorNodeC(ulong objectSerial);

    /* midi */
    [LibraryImport(
        LIB,
        EntryPoint = "frsonic_create_midi_mix_port",
        StringMarshalling = StringMarshalling.Utf8
    )]
    internal static partial ulong CreateMidiMixPortC(
        string pmxPurpose,
        string pmxTag,
        LayerSelectCallback layerCb,
        ChannelSelectCallback channelCb,
        DialModeCallback dialModeCb,
        FilterSectionCallback filterSectionCb
    );

    [LibraryImport(
        LIB,
        EntryPoint = "frsonic_create_mm1_port",
        StringMarshalling = StringMarshalling.Utf8
    )]
    internal static partial ulong CreateMm1PortC(
        string pmxPurpose,
        string pmxTag,
        LayerSelectCallback layerCb,
        ChannelSelectCallback channelCb,
        DialModeCallback dialModeCb,
        FilterSectionCallback filterSectionCb,
        PagesRightCallback pagesRightCb,
        PagesLeftCallback pagesLeftCb
    );

    [LibraryImport(
        LIB,
        EntryPoint = "frsonic_create_fader_fox_pc4_port",
        StringMarshalling = StringMarshalling.Utf8
    )]
    internal static partial ulong CreateFaderFoxPc4PortC(string pmxPurpose, string pmxTag);

    [LibraryImport(
        LIB,
        EntryPoint = "frsonic_create_launchpad_mini_port",
        StringMarshalling = StringMarshalling.Utf8
    )]
    internal static partial ulong CreateLaunchpadMiniPortC(
        string pmxPurpose,
        string pmxTag,
        LayerSelectCallback layerCb,
        LaunchpadLoopPressedCallback loopPressedCb,
        LaunchpadFaderChangedCallback faderChangedCb
    );

    [LibraryImport(LIB, EntryPoint = "frsonic_set_selected_plugin_page")]
    internal static partial void SetSelectedPluginPageC(ulong pluginId, ulong pageNumber);

    [LibraryImport(LIB, EntryPoint = "frsonic_set_selected_channel")]
    internal static partial void SetSelectedChannelC(ChannelType channelType, ulong channelId);

    [LibraryImport(LIB, EntryPoint = "frsonic_clear_selected_channel")]
    internal static partial void ClearSelectedChannelC();

    [LibraryImport(LIB, EntryPoint = "frsonic_set_selected_layer")]
    internal static partial void SetSelectedLayerC(ulong layerId);

    [LibraryImport(LIB, EntryPoint = "frsonic_set_channel_node")]
    internal static partial void SetChannelNodeC(
        ChannelType channelType,
        ulong channelId,
        ulong objectId
    );

    [LibraryImport(LIB, EntryPoint = "frsonic_set_master_channel_node")]
    internal static partial void SetMasterChannelNodeC(ulong layerId, ulong objectId);

    [LibraryImport(LIB, EntryPoint = "frsonic_set_channel_filter_node")]
    internal static partial void SetChannelFilterNodeC(
        ChannelType channelType,
        ulong channelId,
        ulong objectId
    );

    [LibraryImport(LIB, EntryPoint = "frsonic_set_channel_send_node")]
    internal static partial void SetChannelSendNodeC(
        ChannelType channelType,
        ulong channelId,
        ulong sendId,
        ulong objectId
    );

    [LibraryImport(LIB, EntryPoint = "frsonic_clear_filter_parameters")]
    internal static partial void ClearFilterParametersC(ChannelType channelType, ulong channelId);

    [LibraryImport(
        LIB,
        EntryPoint = "frsonic_add_filter_parameter",
        StringMarshalling = StringMarshalling.Utf8
    )]
    internal static partial void AddFilterParameterC(
        ChannelType channelType,
        ulong channelId,
        ulong pluginId,
        string name,
        float min,
        float max
    );

    [LibraryImport(LIB, EntryPoint = "frsonic_set_launchpad_mini_looper_slot_state")]
    internal static partial void SetLaunchpadMiniLooperSlotStateC(
        ChannelType channelType,
        ulong channelId,
        ulong loopPosition,
        [MarshalAs(UnmanagedType.U1)] bool available,
        [MarshalAs(UnmanagedType.U1)] bool loaded,
        [MarshalAs(UnmanagedType.U1)] bool playing
    );

    /* silence producers */
    [LibraryImport(LIB, EntryPoint = "frsonic_create_silence_producer")]
    internal static partial IntPtr CreateSilenceProducerC(ulong targetSerial);

    [LibraryImport(LIB, EntryPoint = "frsonic_destroy_silence_producer")]
    internal static partial void DestroySilenceProducerC(IntPtr handle);

    /* lv2 */
    [LibraryImport(LIB, EntryPoint = "frsonic_lv2_init")]
    internal static partial void Lv2InitC();

    [LibraryImport(LIB, EntryPoint = "frsonic_lv2_destroy")]
    internal static partial void Lv2DestroyC();

    [LibraryImport(LIB, EntryPoint = "frsonic_lv2_plugin_descriptions_json")]
    internal static partial IntPtr Lv2PluginDescriptionsJsonC();

    [LibraryImport(LIB, EntryPoint = "frsonic_lv2_plugin_classes_json")]
    internal static partial IntPtr Lv2PluginClassesJsonC();
}
