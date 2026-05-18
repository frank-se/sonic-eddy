using System.Text.Json.Serialization;

namespace Fr.Sonic.Model.PropInfo;

/// <summary>
/// A collection with all the property infos for a pipewire node
/// </summary>
public class PropertyInfoCollection
{
    /// <summary>
    /// Object Serial of the node the property infos belong to
    /// </summary>
    [JsonPropertyName("objectSerial")]
    public required ulong ObjectSerial { get; init; }

    /// <summary>
    /// Object id  of the node the property infos belong to
    /// </summary>
    [JsonPropertyName("objectId")] public required ulong ObjectId { get; init; }

    /// <summary>
    /// Property infos
    /// </summary>
    [JsonPropertyName("propertyInfos")]
    public required List<PropertyInfo> PropertyInfos { get; init; }
}