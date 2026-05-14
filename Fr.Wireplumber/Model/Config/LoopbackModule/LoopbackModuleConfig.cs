using System.Text.Json.Serialization;

namespace Fr.Wireplumber.Model.Config.LoopbackModule;

/// <summary>
/// Configuration for the loopback module. The loopback module copies the inputs
/// to the outputs. This is useful to map different channels from pro-audio
/// interfaces to streams. It can also be used to create upmix from stereo to
/// 5.1 or 7.1 configurations.
/// </summary>
public class LoopbackModuleConfig
{
    /// <summary>
    /// Properties of the capture node of the loopback module. See
    /// <see cref="NodePropertiesConfig"/> for a description of the properties.
    /// </summary>
    [JsonPropertyName("capture.props")]
    public required NodePropertiesConfig CaptureProps { get; set; }

    /// <summary>
    /// Properties of the playback node of the loopback module. See
    /// <see cref="NodePropertiesConfig"/> for a description of the properties.
    /// </summary>
    [JsonPropertyName("playback.props")]
    public required NodePropertiesConfig PlaybackProps { get; set; }
}