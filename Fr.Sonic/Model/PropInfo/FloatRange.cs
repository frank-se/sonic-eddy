using System.Text.Json.Serialization;

namespace Fr.Sonic.Model.PropInfo;

/// <summary>
/// Describes a continuous range of type float. The value can take on any
/// value in between the minimum and the maximum.
/// </summary>
public class FloatRange : PropertyType
{
    /// <summary>
    /// The default value
    /// </summary>
    [JsonPropertyName("default")]
    public required float Default { get; init; }

    /// <summary>
    /// The minimum value
    /// </summary>
    [JsonPropertyName("minimum")]
    public required float Minimum { get; init; }

    /// <summary>
    /// The maximum value
    /// </summary>
    [JsonPropertyName("maximum")]
    public required float Maximum { get; init; }
}