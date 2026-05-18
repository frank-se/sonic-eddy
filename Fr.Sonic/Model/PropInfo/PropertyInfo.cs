using System.Text.Json.Serialization;

namespace Fr.Sonic.Model.PropInfo;

// ReSharper disable once ClassNeverInstantiated.Global
/// <summary>
/// Describes a property of a pipewire node
/// </summary>
public class PropertyInfo
{
    /// <summary>
    /// Name of the property
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// The description of the property
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }

    /// <summary>
    /// The property type description. This described which values a property
    /// can contain
    /// </summary>
    [JsonPropertyName("propertyType")]
    public PropertyType? PropertyType { get; init; }

    /// <summary>
    /// Indicates if the property is a single value, or if it is in a container,
    /// for example an array.
    /// </summary>
    [JsonPropertyName("container")]
    public string? Container { get; init; }

    /// <summary>
    /// Is <c>true</c> if the property is a parameter. Parameters are dynamic
    /// properties, defined by the node, not by pipewire. For example, the
    /// properties of a plugin in a filter chain module will be exposed as params
    /// </summary>
    [JsonPropertyName("isParam")]
    public required bool IsParam { get; init; }
}