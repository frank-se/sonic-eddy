using System.Text.Json;
using Fr.Lv2.Model;

namespace Fr.Lv2;

public static class Lv2
{
    public static void Init() => Fr.Lv2.PInvoke.FrLv2Lib.Init();
    public static void Destroy() => Fr.Lv2.PInvoke.FrLv2Lib.Destroy();

    public static List<PluginDescription> PluginDescriptions() =>
        JsonSerializer.Deserialize<List<PluginDescription>>(
            Fr.Lv2.PInvoke.FrLv2Lib.PluginDescriptionsJson())!;

    public static List<ClassDescription> ClassDescriptions() =>
        JsonSerializer.Deserialize<List<ClassDescription>>(
            Fr.Lv2.PInvoke.FrLv2Lib.PluginClassesJson())!;
}