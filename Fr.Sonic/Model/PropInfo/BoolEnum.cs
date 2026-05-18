using System.Text.Json.Serialization;

namespace Fr.Sonic.Model.PropInfo;

/// <summary>
/// Describes a property of type bool
/// </summary>
public class BoolEnum : PropertyType
{
    /// <summary>
    /// The default value
    /// </summary>
    [JsonPropertyName("default")] public bool Default { get; init; }

    /// <summary>
    /// The possible values
    /// </summary>
    [JsonPropertyName("values")]
    public required List<bool> Values { get; init; }
}