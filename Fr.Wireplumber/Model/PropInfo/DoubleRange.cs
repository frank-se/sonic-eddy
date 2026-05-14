using System.Text.Json.Serialization;

namespace Fr.Wireplumber.Model.PropInfo;

/// <summary>
/// Describes a continuous range of type double. The value can take on any
/// value in between the minimum and the maximum.
/// </summary>
public class DoubleRange : PropertyType
{
    /// <summary>
    /// The default value
    /// </summary>
    [JsonPropertyName("default")]
    public required double Default { get; init; }

    /// <summary>
    /// The minimum value
    /// </summary>
    [JsonPropertyName("minimum")]
    public required double Minimum { get; init; }

    /// <summary>
    /// The maximum value
    /// </summary>
    [JsonPropertyName("maximum")]
    public required double Maximum { get; init; }
}