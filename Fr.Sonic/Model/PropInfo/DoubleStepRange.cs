using System.Text.Json.Serialization;

namespace Fr.Sonic.Model.PropInfo;

/// <summary>
/// A range with values of type double that can take discretely stepped values
/// between minimum and maximum. The step size describes the increment in which
/// The values can be incremented or decremented.
/// </summary>
public class DoubleStepRange : PropertyType
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

    /// <summary>
    /// The step, the increment in which the value can change
    /// </summary>
    [JsonPropertyName("step")]
    public required double Step { get; init; }
}