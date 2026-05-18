using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Fr.Sonic.Marshalling;
using Fr.Sonic.Model;
using Fr.Sonic.Model.Messages;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Model.Params;
using Fr.Sonic.Model.Params.Marshalling;
using Fr.Sonic.Model.PropInfo;
using Fr.Sonic.Model.Props;
using Fr.Sonic.PInvoke;
using Fr.Sonic.Registries.Devices;
using Fr.Sonic.Registries.Nodes;

namespace Fr.Sonic;

public static class FrSonicWireplumber
{
    /* ── internal callback handlers (called from the PipeWire loop thread) ─ */

    internal static void OnNodeAdded(IntPtr dataPtr)
    {
        var data = Marshal.PtrToStructure<WireplumberData>(dataPtr);

        switch (data.type)
        {
            case wireplumber_object_type.device:
                var deviceTcs = new DeviceTaskCompletionSources();
                FrSonic.UpdatesFromPipewire.Add(
                    new WireplumberDeviceAddedMessage(
                        Device.FromWireplumber(data,
                            deviceTcs.InitialNodesTaskCompletionSource.Task),
                        deviceTcs));
                break;
            case wireplumber_object_type.node:
                var nodeTcs = new NodeTaskCompletionSources();
                FrSonic.UpdatesFromPipewire.Add(new WireplumberNodeAddedMessage(
                    Node.FromWireplumber(data,
                        nodeTcs.InitialPropertyInfoCompletionSource.Task,
                        nodeTcs.InitialParamsTaskCompletionSource.Task,
                        nodeTcs.InitialPropertiesTaskCompletionSource.Task,
                        nodeTcs.DeviceTaskCompletionSource.Task),
                    nodeTcs));
                break;
            case wireplumber_object_type.port:
                FrSonic.UpdatesFromPipewire.Add(
                    new WireplumberPortAddedMessage(Port.FromWireplumber(data)));
                break;
            case wireplumber_object_type.link:
                FrSonic.UpdatesFromPipewire.Add(
                    new WireplumberLinkAddedMessage(Link.FromWireplumber(data)));
                break;
            case wireplumber_object_type.client:
                FrSonic.UpdatesFromPipewire.Add(
                    new WireplumberClientAddedMessage(Client.FromWireplumber(data)));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    internal static void OnPropsChanged(IntPtr propsPtr, IntPtr paramUpdatePtr)
    {
        var data = Marshal.PtrToStructure<PipewirePropsData>(propsPtr);
        var props = Properties.FromPipewireProps(data);
        FrSonic.UpdatesFromPipewire.Add(new PropsChangesMessage(props));
        if (paramUpdatePtr == IntPtr.Zero) return;

        var paramsDict = PipewireParamDataParser.ParseParameterUpdates(paramUpdatePtr);
        FrSonic.UpdatesFromPipewire.Add(
            new ParamsUpdatedMessage(props.ObjectSerial, paramsDict));
    }

    internal static void OnPropsEnumFailed(ulong objectSerial) =>
        FrSonic.UpdatesFromPipewire.Add(new PropsEnumFailedMessage(objectSerial));

    internal static void OnPropInfoAdded(IntPtr jsonPtr)
    {
        var json = Marshal.PtrToStringUTF8(jsonPtr);
        if (json == null) return;
        var propInfo = JsonSerializer.Deserialize<PropertyInfoCollection>(json);
        if (propInfo != null)
            FrSonic.UpdatesFromPipewire.Add(new PropInfosUpdatedMessage(propInfo));
    }

    internal static void OnObjectDeleted(ulong objectSerial,
        wireplumber_object_type objectType) =>
        FrSonic.UpdatesFromPipewire.Add(
            new ObjectDeletedMessage(objectSerial, objectType));

    internal static void OnMetadataAdded(IntPtr metadataName) =>
        FrSonic.UpdatesFromPipewire.Add(
            new MetadataAddedMessage(metadataName.ConvertToString()!));

    internal static void OnMetadataEntryUpdated(IntPtr metadataName,
        ulong subject, IntPtr key, IntPtr type, IntPtr value) =>
        FrSonic.UpdatesFromPipewire.Add(new MetadataEntryUpdatedMessage(
            metadataName.ConvertToString()!, subject, key.ConvertToString()!,
            type.ConvertToString()!, value.ConvertToString()!));

    internal static void OnMetadataEntryDeleted(IntPtr metadataName,
        ulong subject, IntPtr key) =>
        FrSonic.UpdatesFromPipewire.Add(new MetadataEntryDeletedMessage(
            metadataName.ConvertToString()!, subject, key.ConvertToString()!));

    /* ── public mutation API ─────────────────────────────────────────────── */

    public static void SetVolumes(ulong objectId, double[] volumes) =>
        FrSonicLib.SetVolumesC(objectId, volumes, (nuint)volumes.Length);

    public static void SetMute(ulong objectId, bool mute) =>
        FrSonicLib.SetMuteC(objectId, mute);

    public static void SetParams(ulong objectId,
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

                var utf8Bytes = Encoding.UTF8.GetBytes(key + '\0');
                handles[i] = GCHandle.Alloc(utf8Bytes, GCHandleType.Pinned);
                keyPointers[i] = handles[i]!.Value.AddrOfPinnedObject();
            }

            arrayHandle = GCHandle.Alloc(keyPointers, GCHandleType.Pinned);
            FrSonicLib.SetParamsC(objectId,
                arrayHandle.Value.AddrOfPinnedObject(),
                paramTypes, values, (nuint)parameters.Count);
        }
        finally
        {
            foreach (var handle in handles)
                handle?.Free();
            arrayHandle?.Free();
        }
    }

    public static void SetMetadataEntry(string metadataName, ulong subject,
        string key, string type, string value) =>
        FrSonicLib.SetMetadataEntryC(metadataName, subject, key, type, value);

    public static void DeleteMetadataEntry(string metadataName, ulong subject,
        string key) =>
        FrSonicLib.DeleteMetadataEntryC(metadataName, subject, key);

    public static void CreateLinkByPortIds(ulong outPortId, ulong inPortId,
        bool linger) =>
        FrSonicLib.CreateLinkByPortIdsC(outPortId, inPortId, linger);

    public static void DeleteLinkByObjectId(ulong objectId) =>
        FrSonicLib.DeleteLinkByObjectIdC(objectId);
}
