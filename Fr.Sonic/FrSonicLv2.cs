using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using Fr.Sonic.Model.Lv2;
using Fr.Sonic.PInvoke;

namespace Fr.Sonic;

/// <summary>
/// LV2 plugin discovery facade.
/// Call <see cref="Init"/> once before querying plugins; call <see cref="Destroy"/>
/// on shutdown.
/// </summary>
public static class FrSonicLv2
{
    public static void Init()    => FrSonicLib.Lv2InitC();
    public static void Destroy() => FrSonicLib.Lv2DestroyC();

    public static string? PluginDescriptionsJson() =>
        Marshal.PtrToStringUTF8(FrSonicLib.Lv2PluginDescriptionsJsonC());

    public static string? PluginClassesJson() =>
        Marshal.PtrToStringUTF8(FrSonicLib.Lv2PluginClassesJsonC());

    public static List<PluginDescription> PluginDescriptions() =>
        JsonSerializer.Deserialize<List<PluginDescription>>(
            PluginDescriptionsJson() ?? "[]") ?? [];

    public static List<ClassDescription> ClassDescriptions() =>
        JsonSerializer.Deserialize<List<ClassDescription>>(
            PluginClassesJson() ?? "[]") ?? [];
}
