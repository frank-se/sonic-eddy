using System.Text.Json.Serialization;

namespace Fr.Sonic.Compositor;

public sealed record CompositorParams(
    int ActiveSceneIndex,
    IReadOnlyList<CompositorSceneInfo> Scenes);

public sealed record CompositorSceneInfo(
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("file")]
    string File);
