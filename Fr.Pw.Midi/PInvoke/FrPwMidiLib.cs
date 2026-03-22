using System.Runtime.InteropServices;

namespace Fr.Pw.Midi.PInvoke;

// ReSharper disable InconsistentNaming
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void NoteOnCallback(ulong midi_port_id, ulong mapping_id,
    ulong note_number, ulong velocity);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LayerSelectCallback(ulong midi_port_id, ulong layer_id);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void ChannelSelectCallback(ulong midi_port_id,
    ulong channel_id);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void DialSectionModeSelectCallback(ulong midi_port_id,
    ulong channel_id, DialMode dial_mode);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void FilterParamsSectionSelectCallback(ulong midi_port_id,
    ulong channel_id, ulong section_id);

internal static partial class FrPwMidiLib
{
    private const string LIBRARY_NAME = "libfrmidimapper.so.0.0.1";

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
}