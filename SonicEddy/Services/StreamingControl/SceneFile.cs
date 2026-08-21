using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SonicEddy.Services.StreamingControl;

// Mirrors pw-video-compositor/src/scene.hpp's SceneConfig/SceneObject JSON
// schema. The compositor already validates this file at its own startup,
// so a read/parse failure here just means "show nothing for this scene" -
// not a crash.
public sealed record SceneFileObject(
    [property: JsonPropertyName("type")]
    string Type,
    [property: JsonPropertyName("position")]
    IReadOnlyList<int> Position,
    [property: JsonPropertyName("width")]
    int Width,
    [property: JsonPropertyName("height")]
    int Height,
    [property: JsonPropertyName("flip_horizontal")]
    bool FlipHorizontal = false,
    [property: JsonPropertyName("flip_vertical")]
    bool FlipVertical = false,
    [property: JsonPropertyName("rotate")]
    int Rotate = 0,
    [property: JsonPropertyName("target_camera_index")]
    int? TargetCameraIndex = null,
    [property: JsonPropertyName("image_file")]
    string? ImageFile = null)
{
    public int X => Position.Count > 0 ? Position[0] : 0;
    public int Y => Position.Count > 1 ? Position[1] : 0;
    public int Z => Position.Count > 2 ? Position[2] : 0;

    public bool IsCamera => string.Equals(Type, "camera", StringComparison.Ordinal);
    public bool IsImage => string.Equals(Type, "image", StringComparison.Ordinal);
}

public sealed record SceneFileConfig(
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("canvas_width")]
    int CanvasWidth,
    [property: JsonPropertyName("canvas_height")]
    int CanvasHeight,
    [property: JsonPropertyName("objects")]
    IReadOnlyList<SceneFileObject> Objects);

public static class SceneFile
{
    public static async Task<SceneFileConfig?> LoadAsync(string path)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<SceneFileConfig>(stream);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
