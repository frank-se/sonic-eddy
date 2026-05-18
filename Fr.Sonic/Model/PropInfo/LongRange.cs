using System.Text.Json.Serialization;

namespace Fr.Sonic.Model.PropInfo;

/// <summary>
/// Describes a continuous range of type long. The value can take on any
/// value in between the minimum and the maximum.
/// </summary>
public class LongRange : PropertyType
{
    /// <summary>
    /// The default value
    /// </summary>
    [JsonPropertyName("default")]
    public required long Default { get; init; }

    /// <summary>
    /// The minimum value
    /// </summary>
    [JsonPropertyName("minimum")]
    public required long Minimum { get; init; }

    /// <summary>
    /// The maximum value
    /// </summary>
    [JsonPropertyName("maximum")]
    public required long Maximum { get; init; }
}