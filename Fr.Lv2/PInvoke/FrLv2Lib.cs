using System.Runtime.InteropServices;

namespace Fr.Lv2.PInvoke;

internal static partial class FrLv2Lib
{
    private const string LibraryName = "libfrlv2.so.0.0.3";

    [LibraryImport(LibraryName, EntryPoint = "init")]
    private static partial void InitC();

    [LibraryImport(LibraryName, EntryPoint = "destroy")]
    private static partial void DestroyC();

    [LibraryImport(LibraryName, EntryPoint = "plugin_descriptions_json")]
    private static partial IntPtr PluginDescriptionsJsonC();

    [LibraryImport(LibraryName, EntryPoint = "plugin_classes_json")]
    private static partial IntPtr PluginClassesJsonC();

    internal static void Init() => InitC();
    internal static void Destroy() => DestroyC();

    internal static string PluginDescriptionsJson()
    {
        var ptr = PluginDescriptionsJsonC();
        return ptr != IntPtr.Zero
            ? Marshal.PtrToStringUTF8(ptr)!
            : throw new InvalidDataException("C String is null");
    }

    internal static string PluginClassesJson()
    {
        var ptr = PluginClassesJsonC();
        return ptr != IntPtr.Zero
            ? Marshal.PtrToStringUTF8(ptr)!
            : throw new InvalidDataException("C String is null");
    }
}