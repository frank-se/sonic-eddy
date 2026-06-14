using System.Text.Json.Serialization;

namespace Fr.Sonic.Model.Config;

/// <summary>
/// Properties of a capture, or playback node in a pipewire module config.
/// </summary>
public class NodePropertiesConfig
{
    /// <summary>
    /// The name of the node.
    /// </summary>
    [JsonPropertyName("node.name")]
    public string? Name { get; set; }

    /// <summary>
    /// Description of the node. This is normally displayed in user interfaces.
    /// </summary>
    [JsonPropertyName("node.description")]
    public string? Description { get; set; }

    /// <summary>
    /// The session manager will keep the node alive, even if it cannot find a
    /// node to connect to, if this is <c>true</c>. Otherwise, the node, and the
    /// module, will be destroyed if there is no target to connect to,
    /// </summary>
    [JsonPropertyName("node.linger")]
    public bool? Linger { get; set; }

    /// <summary>
    /// The session manager will try to automatically connect the node if this
    /// is <c>true</c>.
    /// </summary>
    [JsonPropertyName("node.autoconnect")]
    public bool? AutoConnect { get; set; }

    /// <summary>
    /// Don't fall back to default connections if the <see cref="TargetObject" />
    /// cannot be found.
    /// </summary>
    [JsonPropertyName("node.dont-fallback")]
    public bool? DontFallback { get; set; }

    /// <summary>
    /// Marks the node as passive if <c>true</c>, which means that the node will
    /// only receive buffers if another node is connected.
    /// </summary>
    [JsonPropertyName("node.passive")]
    public bool? Passive { get; set; }

    /// <summary>
    /// Where the node should be connected to. This can be an
    /// <c>object serial</c> or a <c>node name</c>.
    /// </summary>
    [JsonPropertyName("target.object")]
    public string? TargetObject { get; set; }

    /// <summary>
    /// The media class of the capture or playback node. For modules these would
    /// typically be <c>Stream/Input/Audio</c>, and <c>Stream/Output/Audio</c>,
    /// respectively.
    /// </summary>
    [JsonPropertyName("media.class")]
    public string? MediaClass { get; set; }

    /// <summary>
    /// The media type of the node.
    /// </summary>
    [JsonPropertyName("media.type")]
    public string? MediaType { get; set; }

    /// <summary>
    /// A list of names for the input ports of a capture node, or the output
    /// ports of the playback node, e.g. <c>[ FL, FR ]</c>.
    /// </summary>
    [JsonPropertyName("audio.position")]
    public List<string>? AudioPosition { get; set; }

    /// <summary>
    /// Controls how mono/stereo signals are upmixed to surround channels.
    /// Set to "simple", "psd", or "karma" to enable upmixing; "none" disables it.
    /// </summary>
    [JsonPropertyName("channelmix.upmix-method")]
    public string? ChannelMixUpmixMethod { get; set; }

    /// <summary>
    /// This is used by the library to identify which nodes belong to which
    /// modules.
    /// </summary>
    [JsonPropertyName("pmx.tag")]
    public string? Tag { get; set; }

    /// <summary>
    /// This is used by the library to identify which nodes belong to which
    /// modules.
    /// </summary>
    [JsonPropertyName("pmx.purpose")]
    public string? Purpose { get; set; }
}