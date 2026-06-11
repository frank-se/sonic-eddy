using System.Text.Json.Serialization;

namespace Fr.Sonic.Model.Config.FilterChain;

/// <summary>
/// Configuration for the pipewire filter chain module
/// </summary>
public class FilterChainModuleConfig
{
    /// <summary>
    /// A user readable media name, e.g. the artist and title.
    /// The <c>media.name</c> pipewire property.
    /// </summary>
    [JsonPropertyName("media.name")]
    public string? MediaName { get; set; }

    /// <summary>
    /// Link group of the node. Nodes that belong to the same link group are not
    /// automatically linked together.
    /// The <c>node.link-group</c> pipewire property.
    /// </summary>
    [JsonPropertyName("node.link-group")]
    public string? LinkGroup { get; set; }

    /// <summary>
    /// Properties of the capture node of the filter chain. See
    /// <see cref="NodePropertiesConfig"/> for a description of the properties.
    /// </summary>
    [JsonPropertyName("capture.props")]
    public required NodePropertiesConfig CaptureProps { get; set; }

    /// <summary>
    /// Properties of the playback node of the filter chain. See
    /// <see cref="NodePropertiesConfig"/> for a description of the properties.
    /// </summary>
    [JsonPropertyName("playback.props")]
    public required NodePropertiesConfig PlaybackProps { get; set; }

    /// <summary>
    /// LV2 atom communication buffer size in bytes. Increase when plugins
    /// report insufficient comm-buffersize (e.g. fil4.lv2 needs ~16608).
    /// </summary>
    [JsonPropertyName("lv2.comm-buffersize")]
    public int? Lv2CommBufferSize { get; set; }

    /// <summary>
    /// The filter graph configuration.
    /// </summary>
    [JsonPropertyName("filter.graph")]
    public required FilterGraphConfig FilterGraph { get; set; }
}