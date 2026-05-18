using System.Runtime.InteropServices;

namespace Fr.Sonic.Marshalling;

internal static class Converters
{
    internal static string? ConvertToString(this IntPtr ptr)
    {
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
    }
}