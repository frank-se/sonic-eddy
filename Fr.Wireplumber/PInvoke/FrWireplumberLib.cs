using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Fr.Wireplumber.Marshalling;
using Fr.Wireplumber.Model.Messages;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Model.Params.Marshalling;
using Fr.Wireplumber.Model.PropInfo;
using Fr.Wireplumber.Model.Props;
using Fr.Wireplumber.Registries.Devices;
using Fr.Wireplumber.Registries.Nodes;

// ReSharper disable InconsistentNaming

namespace Fr.Wireplumber.PInvoke;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void WireplumberNodeAddedCallback(IntPtr node);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void PipewirePropsChangedCallback(
    IntPtr propsPtr,
    IntPtr paramUpdatePtr);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void PipewirePropsEnumFailedCallback(ulong objectSerial);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void PipewirePropInfoAddedCallback(IntPtr jsonPtr);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void PipewireObjectDeletedCallback(ulong object_serial,
    wireplumber_object_type object_type);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void MetadataAddedCallback(IntPtr metadata_name);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void MetadataEntryUpdatedCallback(IntPtr metadata_name,
    ulong subject, IntPtr key, IntPtr type, IntPtr value);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void MetadataEntryDeletedCallback(IntPtr metadata_name,
    ulong subject, IntPtr key);

/// <summary>
/// Contains all methods for PInvoke
/// </summary>
internal static partial class FrWireplumberLib
{
    private const string LIBRARY_NAME = "libfrwireplumber.so.0.9.1";

    internal static BlockingCollection<IMessage> UpdatesFromPipewire { get; } =
        new();

    /*
     * Lifetime Management
     */
    [LibraryImport(LIBRARY_NAME, EntryPoint = "init")]
    private static partial void InitC(
        WireplumberNodeAddedCallback nodeAddedAddedCallback,
        PipewirePropsChangedCallback propsChangedCallback,
        PipewirePropsEnumFailedCallback propsEnumFailedCallback,
        PipewirePropInfoAddedCallback propInfoAddedCallback,
        PipewireObjectDeletedCallback objectDeletedCallback,
        MetadataAddedCallback metadataAddedCallback,
        MetadataEntryUpdatedCallback metadataEntryUpdatedCallback,
        MetadataEntryDeletedCallback metadataEntryDeletedCallback);

    [LibraryImport(LIBRARY_NAME, EntryPoint = "start")]
    private static partial void StartC();

    [LibraryImport(LIBRARY_NAME, EntryPoint = "stop")]
    private static partial void StopC();

    /*
     * Pipewire Object Property Handling
     */
    [LibraryImport(LIBRARY_NAME, EntryPoint = "set_volumes")]
    private static partial void SetVolumesC(ulong object_id,
        ReadOnlySpan<double> volumes,
        ulong size);

    [LibraryImport(LIBRARY_NAME, EntryPoint = "set_mute")]
    private static partial void SetMuteC(ulong object_id,
        [MarshalAs(UnmanagedType.U1)] bool mute);

    [LibraryImport(LIBRARY_NAME, EntryPoint = "set_params")]
    private static partial void SetParamsC(ulong id, IntPtr keys,
        ReadOnlySpan<ParamType> param_types, ReadOnlySpan<ulong> values,
        nuint count);

    /*
     * Pipewire Module Handling
     */
    [LibraryImport(LIBRARY_NAME,
        EntryPoint = "load_module",
        StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool LoadModuleC(
        string module_name, string module_config, out IntPtr module_handle);

    [LibraryImport(LIBRARY_NAME, EntryPoint = "destroy_module")]
    private static partial void DestroyModuleC(IntPtr module_handle);

    /*
     * Metadata handling
     */
    [LibraryImport(LIBRARY_NAME, EntryPoint = "set_metadata_entry",
        StringMarshalling = StringMarshalling.Utf8)]
    private static partial void SetMetadataEntryC(string metadata_name,
        ulong subject, string key, string type, string value);

    [LibraryImport(LIBRARY_NAME, EntryPoint = "delete_metadata_entry",
        StringMarshalling = StringMarshalling.Utf8)]
    private static partial void DeleteMetadataEntryC(string metadata_name,
        ulong subject, string key);

    /*
     * Link Management
     */
    [LibraryImport(LIBRARY_NAME, EntryPoint = "create_link_by_port_ids")]
    private static partial void CreateLinkByPortIdC(ulong outputPortId,
        ulong inputPortId, [MarshalAs(UnmanagedType.U1)] bool linger);

    [LibraryImport(LIBRARY_NAME, EntryPoint = "delete_link_by_object_id")]
    private static partial void DeleteLinkByObjectIdC(ulong objectId);

    /*
     * Exports
     */
    internal static void Init() =>
        InitC(NodeAddedCallback, PropsChangedCallback, PropsEnumFailedCallback,
            PropInfoAddedCallback,
            ObjectDeletedCallback, MetadataAddedCallback,
            MetadataEntryUpdatedCallback, MetadataEntryDeletedCallback);

    internal static void Start() => StartC();

    internal static void Stop() => StopC();

    internal static void SetVolumes(ulong objectSerial, double[] volume) =>
        SetVolumesC(objectSerial, volume, (ulong)volume.Length);

    internal static void SetMute(ulong objectId, bool mute) =>
        SetMuteC(objectId, mute);

    internal static void SetParams(ulong objectId,
        List<(string Key, object Value)> parameters)
    {
        if (parameters.Count == 0) return;

        var paramTypes = new ParamType[parameters.Count];
        var values = new ulong[parameters.Count];
        var keyPointers = new IntPtr[parameters.Count];
        var handles = new GCHandle?[parameters.Count];
        GCHandle? arrayHandle = null;

        try
        {
            for (var i = 0; i < parameters.Count; i++)
            {
                var (key, value) = parameters[i];
                paramTypes[i] = value switch
                {
                    bool => ParamType.Bool,
                    int => ParamType.Int,
                    long => ParamType.Long,
                    float => ParamType.Float,
                    double => ParamType.Double,
                    _ => throw new ArgumentException(
                        $"Unsupported value type {value.GetType().Name}")
                };

                values[i] = value switch
                {
                    bool v => v ? 1ul : 0ul,
                    int v => (ulong)v,
                    long v => (ulong)v,
                    float v => BitConverter.SingleToUInt32Bits(v),
                    double v => BitConverter.DoubleToUInt64Bits(v),
                    _ => throw new ArgumentException(
                        $"Unsupported value type {value.GetType().Name}")
                };

                var utf8Bytes =
                    Encoding.UTF8.GetBytes(parameters[i].Key + '\0');
                handles[i] = GCHandle.Alloc(utf8Bytes, GCHandleType.Pinned);
                keyPointers[i] = handles[i]!.Value.AddrOfPinnedObject();
            }

            arrayHandle = GCHandle.Alloc(keyPointers, GCHandleType.Pinned);
            var keysArrayPtr = arrayHandle.Value.AddrOfPinnedObject();

            SetParamsC(objectId, keysArrayPtr, paramTypes, values,
                (nuint)parameters.Count);
        }
        finally
        {
            foreach (var handle in handles)
            {
                handle?.Free();
            }

            arrayHandle?.Free();
        }
    }

    internal static bool LoadModule(string module_name, string module_config,
        out IntPtr module_handle) =>
        LoadModuleC(module_name, module_config, out module_handle);

    internal static void DestroyModule(IntPtr module_handle) =>
        DestroyModuleC(module_handle);

    internal static void SetMetadataEntry(string metadata_name,
        ulong subject, string key, string type, string value) =>
        SetMetadataEntryC(metadata_name, subject, key, type, value);

    internal static void DeleteMetadataEntry(string metadata_name,
        ulong subject, string key) =>
        DeleteMetadataEntryC(metadata_name, subject, key);

    internal static void CreateLinkByPortId(ulong outputPortId,
        ulong inputPortId, bool linger) =>
        CreateLinkByPortIdC(outputPortId, inputPortId, linger);

    internal static void DeleteLinkByObjectId(ulong objectId) =>
        DeleteLinkByObjectIdC(objectId);

    private static void NodeAddedCallback(IntPtr dataPtr)
    {
        var data = Marshal.PtrToStructure<WireplumberData>(dataPtr);

        switch (data.type)
        {
            case wireplumber_object_type.device:
                var deviceTcs = new DeviceTaskCompletionSources();
                UpdatesFromPipewire.Add(
                    new WireplumberDeviceAddedMessage(
                        Device.FromWireplumber(data,
                            deviceTcs.InitialNodesTaskCompletionSource.Task),
                        deviceTcs));
                break;
            case wireplumber_object_type.node:
                var nodeTcs =
                    new NodeTaskCompletionSources();

                UpdatesFromPipewire.Add(new WireplumberNodeAddedMessage(
                    Node.FromWireplumber(data,
                        nodeTcs.InitialPropertyInfoCompletionSource.Task,
                        nodeTcs.InitialParamsTaskCompletionSource.Task,
                        nodeTcs.InitialPropertiesTaskCompletionSource.Task,
                        nodeTcs.DeviceTaskCompletionSource.Task),
                    nodeTcs));
                break;
            case wireplumber_object_type.port:
                var port = Port.FromWireplumber(data);
                UpdatesFromPipewire.Add(
                    new WireplumberPortAddedMessage(port));
                break;
            case wireplumber_object_type.link:
                UpdatesFromPipewire.Add(
                    new WireplumberLinkAddedMessage(
                        Link.FromWireplumber(data)));
                break;
            case wireplumber_object_type.client:
                UpdatesFromPipewire.Add(
                    new WireplumberClientAddedMessage(
                        Client.FromWireplumber(data)));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void PropsChangedCallback(
        IntPtr propsPtr,
        IntPtr paramUpdatePtr)
    {
        var data = Marshal.PtrToStructure<PipewirePropsData>(propsPtr);
        var props = Properties.FromPipewireProps(data);
        UpdatesFromPipewire.Add(new PropsChangesMessage(props));
        if (paramUpdatePtr == IntPtr.Zero) return;

        var paramsDict = PipewireParamDataParser.ParseParameterUpdates(
            paramUpdatePtr);
        UpdatesFromPipewire.Add(
            new ParamsUpdatedMessage(props.ObjectSerial, paramsDict));
    }

    private static void PropsEnumFailedCallback(ulong objectSerial) =>
        UpdatesFromPipewire.Add(new PropsEnumFailedMessage(objectSerial));

    private static void PropInfoAddedCallback(IntPtr jsonPtr)
    {
        var json = Marshal.PtrToStringUTF8(jsonPtr);
        if (json == null) return;
        var propInfo =
            JsonSerializer.Deserialize<PropertyInfoCollection>(json);
        if (propInfo != null)
            UpdatesFromPipewire.Add(new PropInfosUpdatedMessage(propInfo));
    }

    private static void ObjectDeletedCallback(ulong objectSerial,
        wireplumber_object_type objectType) =>
        UpdatesFromPipewire.Add(
            new ObjectDeletedMessage(objectSerial, objectType));

    private static void MetadataAddedCallback(IntPtr metadataName) =>
        UpdatesFromPipewire.Add(
            new MetadataAddedMessage(metadataName.ConvertToString()!));

    private static void MetadataEntryUpdatedCallback(IntPtr metadata_name,
        ulong subject, IntPtr key, IntPtr type, IntPtr value) =>
        UpdatesFromPipewire.Add(new MetadataEntryUpdatedMessage(
            metadata_name.ConvertToString()!, subject, key.ConvertToString()!,
            type.ConvertToString()!, value.ConvertToString()!));

    private static void MetadataEntryDeletedCallback(IntPtr metadata_name,
        ulong subject, IntPtr key) =>
        UpdatesFromPipewire.Add(new MetadataEntryDeletedMessage(
            metadata_name.ConvertToString()!, subject, key.ConvertToString()!));
}