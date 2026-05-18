using System.Text.Json.Serialization;

namespace Fr.Sonic.Model.PropInfo;

/// <summary>
/// A property pr parameter that can take different string values.
/// </summary>
public class StringValues : PropertyType
{
    /// <summary>
    /// Default values
    /// </summary>
    [JsonPropertyName("value")]
    public required string Value { get; init; }

    /// <summary>
    /// Possible values
    /// </summary>
    [JsonPropertyName("labels")]
    public required List<string> Labels { get; init; }
}