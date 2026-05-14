using System.Runtime.InteropServices;

namespace Fr.Wireplumber.PInvoke;

/*
 * This struct is used to pass data from the C++ to the C# side. The order of
 * the fields must match the order of the fields in the C++ struct.
 */
[StructLayout(LayoutKind.Sequential)]
internal struct PipewirePropsData()
{
    internal wireplumber_object_type type = wireplumber_object_type.node;

    internal ulong object_id = 0;
    internal ulong object_serial = 0;
    internal float volume = 0;
    [MarshalAs(UnmanagedType.U1)] internal bool mute = false;
    internal IntPtr channel_volumes;
    internal ulong channel_volumes_size = 0;
    [MarshalAs(UnmanagedType.U1)] internal bool soft_mute = false;
    internal IntPtr soft_volumes;
    internal ulong soft_volumes_size = 0;
    [MarshalAs(UnmanagedType.U1)] internal bool monitor_mute = false;
    internal IntPtr monitor_volumes;
    internal ulong monitor_volumes_size = 0;
    internal IntPtr channel_map;
    internal ulong channel_map_size = 0;
}