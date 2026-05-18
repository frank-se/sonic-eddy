using System.Text.Json.Serialization;

namespace Fr.Sonic.Model.Lv2;

public class PortDescription
{
    [JsonPropertyName("index")] public required ulong Index { get; init; }

    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("symbol")] public required string Symbol { get; init; }

    [JsonPropertyName("minimum")] public float? Minimum { get; init; }

    [JsonPropertyName("maximum")] public float? Maximum { get; init; }

    [JsonPropertyName("default")] public float? Default { get; init; }

    [JsonPropertyName("classes")]
    public required List<string> Classes { get; init; }
}