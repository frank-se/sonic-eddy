using System.Text.Json;
using Fr.Sonic.Model.Params;

namespace Fr.Sonic.Compositor;

internal static class CompositorParamParser
{
    private const string ActiveSceneIndexKey = "active_scene_index";
    private const string ScenesKey = "scenes";

    public static CompositorParams? Parse(Dictionary<string, IParameter>? parameters)
    {
        if (parameters is null)
            return null;

        var hasCompositorParam = false;
        int activeSceneIndex = 0;
        IReadOnlyList<CompositorSceneInfo> scenes = [];

        if (parameters.TryGetValue(ActiveSceneIndexKey, out var indexParameter))
        {
            hasCompositorParam = true;
            activeSceneIndex = ParseActiveSceneIndex(indexParameter) ?? 0;
        }

        if (parameters.TryGetValue(ScenesKey, out var scenesParameter))
        {
            hasCompositorParam = true;
            scenes = ParseScenes(scenesParameter);
        }

        return hasCompositorParam ? new(activeSceneIndex, scenes) : null;
    }

    private static int? ParseActiveSceneIndex(IParameter parameter) =>
        parameter switch
        {
            Parameter<int> value => value.Value,
            Parameter<long> value => (int)value.Value,
            Parameter<float> value => (int)value.Value,
            Parameter<double> value => (int)value.Value,
            _ => null
        };

    private static IReadOnlyList<CompositorSceneInfo> ParseScenes(IParameter parameter)
    {
        if (parameter is not Parameter<string> value ||
            string.IsNullOrWhiteSpace(value.Value))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<CompositorSceneInfo>>(value.Value) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
