using System.Text.Json.Serialization;

namespace Fr.Sonic.Model.Config.FilterChain;

/// <summary>
/// Configuration for a link in the filter graph
/// </summary>
public class FilterGraphLink
{
    /// <summary>
    /// Name of the output port.
    /// </summary>
    [JsonPropertyName("output")]
    public required string Output { get; set; }

    /// <summary>
    /// Name of the input port.
    /// </summary>
    [JsonPropertyName("input")]
    public required string Input { get; set; }
}