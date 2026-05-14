using System.Text.Json.Serialization;

namespace Fr.Lv2.Model;

public class ClassDescription
{
    [JsonPropertyName("uri")] public required string Uri { get; init; }

    [JsonPropertyName("label")] public required string Label { get; init; }

    [JsonPropertyName("parentUri")] public string? ParentUri { get; init; }
}