using Fr.Sonic.Marshalling;
using Fr.Sonic.Model.Metadata;
using Fr.Sonic.Model.Params;
using Fr.Sonic.Model.PropInfo;
using Fr.Sonic.Model.Props;
using Fr.Sonic.PInvoke;

// ReSharper disable NotAccessedPositionalProperty.Global

namespace Fr.Sonic.Model.Objects;

/// <summary>
/// Alsa information for the node
/// </summary>
/// <param name="AutoLink"></param>
/// <param name="DisableBatch"></param>
/// <param name="DisableMemoryMap"></param>
/// <param name="DisableTSched"></param>
/// <param name="Headroom"></param>
/// <param name="HTimeStamp"></param>
/// <param name="HTimeStampMaxErrors"></param>
/// <param name="MultiRate"></param>
/// <param name="Path"></param>
/// <param name="PeriodNum"></param>
/// <param name="PeriodSize"></param>
/// <param name="StartDelay"></param>
/// <param name="UseChannelMap"></param>
public record AlsaNode(
    bool AutoLink,
    bool DisableBatch,
    bool DisableMemoryMap,
    bool DisableTSched,
    ulong Headroom,
    bool HTimeStamp,
    ulong HTimeStampMaxErrors,
    bool MultiRate,
    string? Path,
    ulong? PeriodNum,
    ulong? PeriodSize,
    ulong StartDelay,
    bool UseChannelMap
);

/// <summary>
/// Audio information for the node
/// </summary>
/// <param name="AllowedRates"></param>
/// <param name="Channels"></param>
/// <param name="Rate"></param>
/// <param name="Format"></param>
public record AudioNode(
    string? AllowedRates,
    ulong Channels,
    ulong Rate,
    string? Format
);

/// <summary>
/// Channel mixer information
/// </summary>
/// <param name="Disable">
///     Disable the channel mixer
/// </param>
/// <param name="FrontChannelCutOff">
///     Front channel filter cut-off
/// </param>
/// <param name="HilbertTaps">
///     Hilbert taps
/// </param>
/// <param name="LowFrequencyEffectsCutOff">
///     LFE channel filter cut-off
/// </param>
/// <param name="LockVolumes">
///     Lock volumes
/// </param>
/// <param name="MaxVolume">
///     Maximum mixer volume
/// </param>
/// <param name="MinVolume">
///     Minimum mixer volume
/// </param>
/// <param name="MixLowFrequencyEffects">
///     Mix low frequency channel into the front, and front center channels.
///     This can be used to increase the dynamic range if no subwoofer is
///     available, and the speakers can handle the low frequencies.
/// </param>
/// <param name="Normalize">
///     Make sure that during mixing, the original 0 dB level is preserved.
/// </param>
/// <param name="UpMix">
///     Upmix the stereo channel front channel to the center, the back, and the
///     low frequency channel.
/// </param>
/// <param name="UpMixMethod">
///     The upmix method defines how the rear channel signal is derived:
///     <list type="bullet">
///         <item><description>
///             <c>none</c> No rear channels are produced
///         </description></item>
///         <item><description>
///             <c>simple</c> Front channels are copied to the rear. Fast but
///             can produce phasing effects
///         </description></item>
///         <item><description>
///             <c>psd</c> Rear channels are produced from the front left, and
///             right ambient sound (the difference between the channels).
///         </description></item>
///     </list>
/// </param>
public record ChannelMixNode(
    bool Disable,
    ulong FrontChannelCutOff,
    double HilbertTaps,
    ulong LowFrequencyEffectsCutOff,
    bool LockVolumes,
    double MaxVolume,
    double MinVolume,
    bool MixLowFrequencyEffects,
    bool Normalize,
    bool UpMix,
    string? UpMixMethod
);

/// <summary>
/// Clock information
/// </summary>
/// <param name="Name">
///     The name of the clock. The clock name is generated from the card index
///     and stream direction. Devices with the same clock name will not use the
///     resampler to align the clocks.
/// </param>
public record ClockNode(string? Name);

/// <summary>
/// Debug information
/// </summary>
/// <param name="Path">
///     Tells the stream to also write the raw samples to the file given by the
///     path.
/// </param>
public record DebugNode(string? Path);

/// <summary>
/// Device information for the node
/// </summary>
/// <param name="Id">Id of the device for the node</param>
public record DeviceNode(ulong Id);

/// <summary>
/// Describe the dither method used by the node
/// </summary>
/// <param name="Method">
///     The dither method used (default <c>none</c>:
///     <list type="bullet">
///         <item><description>
///             <c>none</c> No dithering is used.
///         </description></item>
///         <item><description>
///             <c>rectangular</c> Dithering  with a rectangular noise
///             distribution. This adds random bits in the [-0.5, 0.5] range to
///             the signal with even distribution.
///         </description></item>
///         <item><description>
///             <c>triangular</c> triangular Dithering with a triangular noise
///             distribution. This adds random bits in the [-1.0, 1.0] range to
///             the signal with triangular distribution around 0.0.
///         </description></item>
///         <item><description>
///             <c>triangular-hf</c> Dithering with a sloped triangular noise
///             distribution. 
///         </description></item>
///         <item><description>
///             <c>wannamaker3</c> Additional noise shaping is performed on the
///             sloped triangular dithering to move the noise to the more
///             inaudible range. This is using the "F-Weighted" noise filter
///             described by Wannamaker.
///         </description></item>
///         <item><description>
///             <c>shaped5</c> Additional noise shaping is performed on the
///             triangular dithering to move the noise to the more inaudible
///             range. This is using the Lipshitz filter.
///         </description></item>
///     </list>
/// </param>
/// <param name="Noise"></param>
public record DitherNode(string? Method, ulong Noise);

/// <summary>
/// Internal latency information for the node
/// </summary>
/// <param name="Nanoseconds">Systemic latency in nanoseconds</param>
/// <param name="Rate">Systemic latency in samples at playback rate</param>
public record InternalLatency(ulong Nanoseconds, ulong Rate);

/// <summary>
/// Media information for the node
/// </summary>
/// <param name="Artist">Artist information</param>
/// <param name="Category">
///     What kind of processing is done with the media:
///     <list type="bullet">
///         <item><description>
///             <c>Playback</c>: Media playback.
///         </description></item>
///         <item><description>
///             <c>Capture</c>: Media capture.
///         </description></item>
///         <item><description>
///             <c>Duplex</c>: Media capture and playback, or general
///             processing.
///         </description></item>
///         <item><description>
///             <c>Monitor</c>: A media monitor application. Does not actively
///             change media data, but monitors activity.
///         </description></item>
///         <item><description>
///             <c>Manager</c>: Manages the media graph
///         </description></item>
///     </list>
/// </param>
/// <param name="Class">
///     Classifies the stream function, possible values are:
///     <list type="bullet">
///         <item><description>
///             <c>Video/Source</c>: A producer of video, like a webcam.
///         </description></item>
///         <item><description>
///             <c>Video/Sink</c>: A consumer of video, like a display window.
///         </description></item>
///         <item><description>
///             <c>Audio/Source</c>: A source of audio samples like a microphone.
///         </description></item>
///         <item><description>
///             <c>Audio/Sink</c>: A sink for audio samples, like an audio card.
///         </description></item>
///         <item><description>
///             <c>Audio/Duplex</c>: A node that is both a sink and a source.
///         </description></item>
///         <item><description>
///             <c>Stream/Output/Audio</c>: A playback stream.
///         </description></item>
///         <item><description>
///             <c>Stream/Input/Audio</c>: A capture stream.
///         </description></item>
///     </list>
/// </param>
/// <param name="Comment"></param>
/// <param name="Copyright"></param>
/// <param name="Date"></param>
/// <param name="FileName"></param>
/// <param name="Format"></param>
/// <param name="Icon"></param>
/// <param name="IconName"></param>
/// <param name="Language"></param>
/// <param name="Name"></param>
/// <param name="Role">
///     Use case of the media, possible values:
///     <list>
///         <item><description>
///             <c>Movie</c>: Movie playback with audio and video.
///         </description></item>
///         <item><description>
///             <c>Music</c>: Music listening.
///         </description></item>
///         <item><description>
///             <c>Camera</c>: Recording video from a camera.
///         </description></item>
///         <item><description>
///             <c>Screen</c>: Recording or sharing the desktop screen.
///         </description></item>
///         <item><description>
///             <c>Communication</c>: VOIP or other video chat application.
///         </description></item>
///         <item><description>
///             <c>Game</c>: Game
///         </description></item>
///         <item><description>
///             <c>Notification</c>: System notification sounds.
///         </description></item>
///         <item><description>
///             <c>DSP</c>: Audio or Video filters and effect processing.
///         </description></item>
///         <item><description>
///             <c>Production</c>: Professional audio processing and production.
///         </description></item>
///         <item><description>
///             <c>Accessibility</c>: Audio and Visual aid for accessibility.
///         </description></item>
///         <item><description>
///             <c>Test</c>: Test program.
///         </description></item>
///     </list>
/// </param>
/// <param name="Software"></param>
/// <param name="Title"></param>
/// <param name="Type"></param>
public record MediaNode(
    string? Artist,
    string? Category,
    string? Class,
    string? Comment,
    string? Copyright,
    string? Date,
    string? FileName,
    string? Format,
    string? Icon,
    string? IconName,
    string? Language,
    string? Name,
    string? Role,
    string? Software,
    string? Title,
    string? Type
);

/// <summary>
/// General information shared by objects
/// </summary>
/// <param name="Linger">
///     Signals that the object should linger. The session manager keeps the
///     object around, if it cannot find a node to connect to. Otherwise, it
///     will be destroyed.
/// </param>
public record ObjectNode(bool Linger);

/// <summary>
/// Configuration for the resampler.
/// </summary>
/// <param name="Disable">
///     Disable the resampler, the node will only be able to connect to the
///     graph if it has the exact sample rate of the graph.
/// </param>
/// <param name="Peaks"></param>
/// <param name="Prefill"></param>
/// <param name="Quality">
///     The quality of the resampler from 0 to 14. The default is 4.
///     Increasing the quality will result in better cutoff and less aliasing
///     at the expense of (much) more CPU consumption. The default quality of 4
///     has been selected as a good compromise between quality and performance
///     with no artifacts that are well below the audible range.
/// </param>
public record NodeResample(
    bool Disable,
    bool Peaks,
    bool Prefill,
    ulong Quality
);

/// <summary>
/// Pipewire loop information.
/// </summary>
/// <param name="Class">
/// Add the node to a specific loop name or loop class. By default, the node is
/// added to the data.rt loop class. You can make more specific data loops and
/// then assign the nodes to those. Other well known names are main-loop.0 and
/// the main node.loop.class which runs the node data processing in the
/// main loop.
/// </param>
/// <param name="Name">null</param>
public record LoopNode(string? Class, string? Name);

/// <summary>
/// Id of the client the node belongs to
/// </summary>
/// <param name="Id">Object Id of the client</param>
public record ClientNode(ulong Id);

/// <summary>
/// Pmx internal information, this is used to find the nodes for the modules it
/// creates.
/// </summary>
/// <param name="Tag"></param>
/// <param name="Purpose"></param>
public record Pmx(string? Tag, string? Purpose);

/// <summary>
/// A pipewire node.
/// </summary>
/// <param name="ObjectId">Object Id</param>
/// <param name="ObjectSerial">Object Serial</param>
/// <param name="MonitorChannelVolumes">
/// Should the monitor ports output reflect the channel volume settings?
/// </param>
/// <param name="AlwaysProcess">
/// The node will always be connected to a driver, even if no port is otherwise
/// connected. This assures that the node always processes. Required for Jack
/// nodes which always need their processing callback to be called.
/// </param>
/// <param name="AutoConnect">
/// The session manager will automatically connect the node if true
/// </param>
/// <param name="Description">
/// Description of the node
/// </param>
/// <param name="Disabled">
/// Disable the creation of this node in session manager
/// </param>
/// <param name="DoNotReconnect">
/// Do not reconnect the node if the configured target is destroyed. The node
/// will insetad be desctroyed.
/// </param>
/// <param name="Exclusive">
/// If the node wants to be exclusively connected to the sink our source.
/// </param>
/// <param name="ForceQuantum"></param>
/// <param name="ForceRate"></param>
/// <param name="LinkGroup">
/// The link group of the node. All nodes assigned to a specific link group will
/// never be linked to each other by the session manager.
/// </param>
/// <param name="LockQuantum"></param>
/// <param name="LockRate"></param>
/// <param name="Name">Name of the node</param>
/// <param name="Passive"></param>
/// <param name="PauseOnIdle"></param>
/// <param name="Rate"></param>
/// <param name="SuspendOnIdle"></param>
/// <param name="WantDriver"></param>
/// <param name="PriorityDriver"></param>
/// <param name="PrioritySession"></param>
/// <param name="DoNotRemixStream"></param>
/// <param name="TargetObject"></param>
/// <param name="Object"><see cref="ObjectNode" /></param>
/// <param name="Alsa"><see cref="AlsaNode" /></param>
/// <param name="Audio"><see cref="AudioNode" /></param>
/// <param name="ChannelMix"><see cref="ChannelMixNode" /></param>
/// <param name="Clock"><see cref="ClockNode" /></param>
/// <param name="Client"><see cref="ClientNode" /></param>
/// <param name="Debug"><see cref="DebugNode" /></param>
/// <param name="DeviceAssignment"><see cref="DeviceNode" /></param>
/// <param name="Dither"><see cref="DitherNode" /></param>
/// <param name="InternalLatency"><see cref="InternalLatency" /></param>
/// <param name="Media"><see cref="MediaNode" /></param>
/// <param name="Resample"><see cref="NodeResample" /></param>
/// <param name="Loop"><see cref="LoopNode" /></param>
/// <param name="Pmx"><see cref="Pmx" /></param>
/// <param name="PropertyInfos"><see cref="PropertyInfos"/></param>
/// <param name="Device">Information about the device</param>
/// <param name="Metadata">Metadata assigned to the node</param>
public record Node(
    ulong ObjectId,
    ulong ObjectSerial,
    bool MonitorChannelVolumes,
    bool AlwaysProcess,
    bool AutoConnect,
    string? Description,
    bool Disabled,
    bool DoNotReconnect,
    bool Exclusive,
    ulong? ForceQuantum,
    ulong? ForceRate,
    string? LinkGroup,
    bool LockQuantum,
    bool LockRate,
    string? Name,
    bool Passive,
    bool PauseOnIdle,
    ulong? Rate,
    bool SuspendOnIdle,
    bool WantDriver,
    ulong? PriorityDriver,
    ulong? PrioritySession,
    bool DoNotRemixStream,
    string? TargetObject,
    ObjectNode Object,
    AlsaNode Alsa,
    AudioNode Audio,
    ChannelMixNode ChannelMix,
    ClockNode Clock,
    ClientNode? Client,
    DebugNode Debug,
    DeviceNode? DeviceAssignment,
    DitherNode Dither,
    InternalLatency InternalLatency,
    MediaNode Media,
    NodeResample Resample,
    LoopNode Loop,
    Pmx Pmx,
    Task<PropertyInfoCollection> PropertyInfos,
    Task<Device> Device,
    MetadataCollection Metadata
) : IWireplumberObject
{
    /// <summary>
    /// Params for the node, <see cref="IParameter"/>
    /// </summary>
    public required Task<Dictionary<string, IParameter>?> Params { get; set; }

    /// <summary>
    /// <see cref="Properties"/>
    ///
    /// Properties enumeration can fail. In that case the properties task will
    /// return <c>null</c>.
    /// </summary>
    public required Task<Properties?> Properties { get; set; }

    /// <summary>
    /// <inheritdoc cref="IWireplumberObject.Deleted" />
    /// </summary>
    public event Action? Deleted;

    /// <summary>
    /// Event is triggered when the properties changed.
    /// </summary>
    public event Action<Properties?>? PropertiesChanged;

    /// <summary>
    /// Event is triggered when the params changed.
    /// </summary>
    public event Action<Dictionary<string, IParameter>?>? ParamsChanged;

    /// <summary>
    /// Mute the node
    /// </summary>
    /// <param name="muted"></param>
    public void SetMute(bool muted)
    {
        FrSonicWireplumber.SetMute(ObjectId, muted);
    }

    /// <summary>
    /// Set volume for the node
    /// </summary>
    /// <param name="volumes"></param>
    public void SetVolumes(double[] volumes)
    {
        FrSonicWireplumber.SetVolumes(ObjectId, volumes);
    }

    /// <summary>
    /// Set a bool parameter
    /// </summary>
    /// <param name="name">Name of the parameter</param>
    /// <param name="value">Value of the parameter</param>
    public void SetParam(string name, bool value)
    {
        FrSonicWireplumber.SetParams(ObjectId, [new(name, value)]);
    }

    /// <summary>
    /// Set an int parameter
    /// </summary>
    /// <param name="name">Name of the parameter</param>
    /// <param name="value">Value of the parameter</param>
    public void SetParam(string name, int value)
    {
        FrSonicWireplumber.SetParams(ObjectId, [new(name, value)]);
    }

    /// <summary>
    /// Set a long parameter
    /// </summary>
    /// <param name="name">Name of the parameter</param>
    /// <param name="value">Value of the parameter</param>
    public void SetParam(string name, long value)
    {
        FrSonicWireplumber.SetParams(ObjectId, [new(name, value)]);
    }

    /// <summary>
    /// Set a float parameter
    /// </summary>
    /// <param name="name">Name of the parameter</param>
    /// <param name="value">Value of the parameter</param>
    public void SetParam(string name, float value)
    {
        FrSonicWireplumber.SetParams(ObjectId, [new(name, value)]);
    }

    /// <summary>
    /// Set a double parameter
    /// </summary>
    /// <param name="name">Name of the parameter</param>
    /// <param name="value">Value of the parameter</param>
    public void SetParam(string name, double value)
    {
        FrSonicWireplumber.SetParams(ObjectId, [new(name, value)]);
    }

    /// <summary>
    /// Set a string parameter
    /// </summary>
    /// <param name="name">Name of the parameter</param>
    /// <param name="value">Value of the parameter</param>
    public void SetParam(string name, string value)
    {
        FrSonicWireplumber.SetParams(ObjectId, [new(name, value)]);
    }

    /// <summary>
    /// Override the target object with metadata
    /// </summary>
    /// <param name="targetObject">
    /// The new target object for the node, can be a node's object serial or its
    /// name.
    /// </param>
    public void OverrideTargetObject(string targetObject) =>
        FrSonicWireplumber.SetMetadataEntry("default", ObjectId,
            "target.object", "Spa:String:JSON", targetObject);

    /// <summary>
    /// <inheritdoc cref="IWireplumberObject.TriggerDeletedEvent" />
    /// </summary>
    public void TriggerDeletedEvent() => Deleted?.Invoke();

    internal void TriggerPropertiesChangedEvent(Properties? properties) =>
        PropertiesChanged?.Invoke(properties);

    internal void TriggerParamsChangedEvent(
        Dictionary<string, IParameter>? parameters) =>
        ParamsChanged?.Invoke(parameters);

    internal static Node FromWireplumber(WireplumberData data,
        Task<PropertyInfoCollection> propertyInfos,
        Task<Dictionary<string, IParameter>?> paramsDict,
        Task<Properties?> properties, Task<Device> device)
    {
        return new(
            data.object_id,
            data.object_serial,
            data.monitor_channel_volumes,
            data.node_always_process,
            data.node_autoconnect,
            data.node_description.ConvertToString(),
            data.node_disabled,
            data.node_dont_reconnect,
            data.node_exclusive,
            data.node_force_quantum,
            data.node_force_rate,
            data.link_group.ConvertToString(),
            data.node_lock_quantum,
            data.node_lock_rate,
            data.node_name.ConvertToString(),
            data.node_passive,
            data.node_pause_on_idle,
            data.node_rate,
            data.node_suspend_on_idle,
            data.node_want_driver,
            data.priority_driver,
            data.priority_session,
            data.stream_dont_remix,
            data.target_object.ConvertToString(),
            new(data.object_linger),
            new(
                data.api_alsa_auto_link,
                data.api_alsa_disable_batch,
                data.api_alsa_disable_mmap,
                data.api_alsa_disable_tsched,
                data.api_alsa_headroom,
                data.api_alsa_htimestamp,
                data.api_alsa_htimestamp_max_errors,
                data.api_alsa_multi_rate,
                data.api_alsa_path.ConvertToString(),
                data.api_alsa_period_num,
                data.api_alsa_period_size,
                data.api_alsa_start_delay,
                data.api_alsa_use_chmap),
            new(
                data.audio_allowed_rates.ConvertToString(),
                data.audio_channels,
                data.audio_rate,
                data.audio_format.ConvertToString()
            ),
            new(
                data.channel_mix_disable,
                data.channel_mix_fc_cutoff,
                data.channel_mix_hilbert_taps,
                data.channel_mix_lfe_cutoff,
                data.channel_mix_lock_volumes,
                data.channel_mix_max_volume,
                data.channel_mix_min_volume,
                data.channel_mix_mix_lfe,
                data.channel_mix_normalize,
                data.channel_mix_upmix,
                data.channel_mix_upmix_method.ConvertToString()
            ),
            new(data.clock_name.ConvertToString()),
            data.client_id != 0
                ? new(data.client_id)
                : null,
            new(data.debug_wav_path.ConvertToString()),
            data.device_id != 0
                ? new(data.device_id)
                : null,
            new(
                data.dither_method.ConvertToString(),
                data.dither_noise
            ),
            new(
                data.latency_internal_rate,
                data.latency_internal_ns
            ),
            new(
                data.media_artist.ConvertToString(),
                data.media_category.ConvertToString(),
                data.media_class.ConvertToString(),
                data.media_comment.ConvertToString(),
                data.media_copyright.ConvertToString(),
                data.media_date.ConvertToString(),
                data.media_filename.ConvertToString(),
                data.media_format.ConvertToString(),
                data.media_icon.ConvertToString(),
                data.media_icon_name.ConvertToString(),
                data.media_language.ConvertToString(),
                data.media_name.ConvertToString(),
                data.media_role.ConvertToString(),
                data.media_software.ConvertToString(),
                data.media_title.ConvertToString(),
                data.media_type.ConvertToString()
            ),
            new(
                data.resample_disable,
                data.resample_peaks,
                data.resample_prefill,
                data.resample_quality
            ),
            new(
                data.node_loop_class.ConvertToString(),
                data.node_loop_name.ConvertToString()
            ),
            new(
                data.pmx_tag.ConvertToString(),
                data.pmx_purpose.ConvertToString()
            ),
            propertyInfos,
            device,
            new("default")
        )
        {
            Params = paramsDict,
            Properties = properties
        };
    }
}
