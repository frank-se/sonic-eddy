using System.Text.Json.Serialization;

namespace Fr.Sonic.Model.Config.FilterChain;

/// <summary>
/// Configuration for the filter graph in the filter chain module.
/// </summary>
public class FilterGraphConfig()
{
    /// <summary>
    /// Describes all the processing nodes in the filter graph. See
    /// <see cref="FilterGraphNode"/> for a description of the properties for
    /// each node.
    /// </summary>
    [JsonPropertyName("nodes")]
    public required List<FilterGraphNode> Nodes { get; set; }

    /// <summary>
    /// Describes a link in the filter graph. See <see cref="FilterGraphLink"/>
    /// for a description of the properties for each link.
    /// </summary>
    [JsonPropertyName("links")]
    public required List<FilterGraphLink> Links { get; set; }

    /// <summary>
    /// List of inputs of the filter chain. The names here reference port names
    /// of the filter chain nodes. The number of entries here defined the number
    /// of ports the filter chain's capture node will have.
    /// You can pass <c>"null"</c> to create an input that will be ignored.
    /// One input can only be mapped to one port, if duplication is necessary,
    /// the copy builtin filter can be used to copy the input.
    /// If the list is omitted, the input ports will be all the input ports of
    /// the first filter.
    /// </summary>
    [JsonPropertyName("inputs")]
    public List<string>? Inputs { get; set; }

    /// <summary>
    /// List of outputs of the filter chain. The names here reference port names
    /// of the filter chain nodes. The number of entries here defined the number
    /// of ports the filter chain's playback node will have.
    /// You can pass <c>"null"</c> to create an output that will be ignored.
    /// One output can only be mapped to one port, if duplication is necessary,
    /// the copy builtin filter can be used to copy the output.
    /// If the list is omitted, the output ports will be all the output ports of
    /// the last filter.
    /// </summary>
    [JsonPropertyName("outputs")]
    public List<string>? Outputs { get; set; }
}