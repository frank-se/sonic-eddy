using System.Text.Json.Serialization;

namespace Fr.Lv2.Model;

public class PluginDescription
{
    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("uri")] public required string Uri { get; init; }

    [JsonPropertyName("pluginClassUri")]
    public required string ClassUri { get; init; }

    [JsonPropertyName("ports")]
    public required List<PortDescription> Ports { get; init; }
}