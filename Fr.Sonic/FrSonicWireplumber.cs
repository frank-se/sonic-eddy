using System.Runtime.InteropServices;
using Fr.Sonic.PInvoke;

namespace Fr.Sonic;

/// <summary>
/// WirePlumber / PipeWire object model facade.
/// Callback handlers post raw events to <see cref="FrSonic.WireplumberEvents"/>;
/// a separate consumer thread dispatches them to the typed registries.
/// The model types and registries mirror those in the old Fr.Wireplumber project
/// and are migrated here as part of the consolidation.
/// </summary>
public static class FrSonicWireplumber
{
    /* ── internal callback handlers (called from the PipeWire loop thread) ─ */

    internal static void OnNodeAdded(IntPtr node)
        => FrSonic.WireplumberEvents.Add(("node_added", node));

    internal static void OnPropsChanged(IntPtr propsPtr, IntPtr paramUpdatePtr)
        => FrSonic.WireplumberEvents.Add(("props_changed", propsPtr, paramUpdatePtr));

    internal static void OnPropsEnumFailed(ulong objectSerial)
        => FrSonic.WireplumberEvents.Add(("props_enum_failed", objectSerial));

    internal static void OnPropInfoAdded(IntPtr jsonPtr)
        => FrSonic.WireplumberEvents.Add(("prop_info_added", jsonPtr));

    internal static void OnObjectDeleted(ulong objectSerial, WireplumberObjectType type)
        => FrSonic.WireplumberEvents.Add(("object_deleted", objectSerial, type));

    internal static void OnMetadataAdded(IntPtr metadataName)
        => FrSonic.WireplumberEvents.Add(("metadata_added", metadataName));

    internal static void OnMetadataEntryUpdated(IntPtr metadataName, ulong subject,
        IntPtr key, IntPtr type, IntPtr value)
        => FrSonic.WireplumberEvents.Add(
            ("metadata_entry_updated", metadataName, subject, key, type, value));

    internal static void OnMetadataEntryDeleted(IntPtr metadataName, ulong subject,
        IntPtr key)
        => FrSonic.WireplumberEvents.Add(
            ("metadata_entry_deleted", metadataName, subject, key));

    /* ── public mutation API ─────────────────────────────────────────────── */

    public static void SetVolumes(ulong objectId, double[] volumes) =>
        FrSonicLib.SetVolumesC(objectId, volumes, (nuint)volumes.Length);

    public static void SetMute(ulong objectId, bool mute) =>
        FrSonicLib.SetMuteC(objectId, mute);

    public static bool LoadModule(string name, string config, out IntPtr handle) =>
        FrSonicLib.LoadModuleC(name, config, out handle);

    public static void DestroyModule(IntPtr handle) =>
        FrSonicLib.DestroyModuleC(handle);

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
