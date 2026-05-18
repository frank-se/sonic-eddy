using System.Text.Json.Serialization;

namespace Fr.Sonic.Model.Config.FilterChain;

/// <summary>
/// Configuration for a node in the filter graph
/// </summary>
public class FilterGraphNode
{
    /// <summary>
    /// The name of the node in the graph. This can be used to refence the node
    /// in the other parts of the filter config, e.g. when describing links,
    /// input node, or output nodes.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// One of <c>ladspa</c>, <c>lv2</c>, <c>builtin</c>, <c>sofa</c>, or
    /// <c>ebur128</c>.
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>
    /// The name of the plugin, depending on the type:
    /// <list type="bullet">
    ///     <item>
    ///         <description>
    ///             For <c>ladspa</c> plugins the filter chain
    ///             appends <c>.so</c> to the name and loads that library.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <description>
    ///             For <c>lv2</c> this is the uri of the plugin.
    ///             Use <c>lv2ls</c> to get the uri.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <description>
    ///             For <c>builtin</c>, <c>sofa</c>, and <c>ebur128</c> this is
    ///             ignored
    ///         </description>
    ///     </item>
    /// </list>
    /// </summary>
    [JsonPropertyName("plugin")]
    public required string Plugin { get; set; }

    /// <summary>
    /// The initial control values, given as a dictionary with the port name as
    /// the kwy, and the value as value.
    /// </summary>
    [JsonPropertyName("control")]
    public Dictionary<string, object>? Control { get; set; }
}