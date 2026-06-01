using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fr.Sonic.PInvoke;
using Fr.Sonic.Model.Config;
using Fr.Sonic.Model.Config.FilterChain;
using Fr.Sonic.Model.Config.Looper;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Conversions;
using SonicEddy.Services.Midi;
using SonicEddy.Services.Wireplumber;

namespace SonicEddy.Services.MixerServiceV2;

public class MixerEditor(IWireplumberService wireplumberService)
{

    public async Task<MixerLayer> AddFilterToChannelStrip(
        MixerLayer mixerLayer,
        ulong channelId,
        FilterGraph filterGraph)
    {
        var channel =
            mixerLayer.Channels.First(c => c.ChannelId == channelId);

        var filterChainConfig = new FilterChainModuleConfig()
        {
            CaptureProps = new()
            {
                Name = $"mixer-fc-{channelId}-capture",
                Description =
                    $"Capture Node for Mixer Filter Channel {channelId}",
                Linger = true,
                AutoConnect = true,
                DontFallback = true,
                Passive = false,
                TargetObject = channel.PreFxLooper.PlaybackNode.ObjectSerial
                    .ToString(),
                MediaClass = "Stream/Input/Audio",
                AudioPosition = ["FL", "FR"]
            },
            PlaybackProps = new()
            {
                Name = $"mixer-fc-{channelId}-playback",
                Description =
                    $"Playback Node for Mixer Filter Channel {channelId}",
                Linger = true,
                AutoConnect = true,
                DontFallback = true,
                Passive = false,
                TargetObject = channel.OutputLoopback.CaptureNode.ObjectSerial
                    .ToString(),
                MediaClass = "Stream/Output/Audio",
                AudioPosition = ["FL", "FR"]
            },
            FilterGraph = filterGraph.ToFilterGraphConfig()
        };

        var filterChain =
            await Fr.Sonic.FrSonic.ModuleFactory
                .CreateFilterChainAsync(
                    $"mixer-fc-{channelId}", filterChainConfig);

        channel.PreFxLooper.PlaybackNode.OverrideTargetObject(
            filterChain.CaptureNode.ObjectSerial.ToString());

        channel.OutputLoopback.CaptureNode.OverrideTargetObject(
            filterChain.PlaybackNode.ObjectSerial.ToString());

        foreach (var send in channel.SendLoopbacks)
        {
            send.CaptureNode.OverrideTargetObject(filterChain.PlaybackNode
                .ObjectSerial.ToString());
        }

        var newChannel = channel with
        {
            FilterChain = filterChain,
            FilterGraph = filterGraph
        };

        var newList = mixerLayer.Channels.Select(c =>
            c.ChannelId == channelId ? newChannel : c).ToList();

        return mixerLayer with
        {
            Channels = newList
        };
    }

    public async Task<MixerLayer> AddFilterToGroupChannel(
        MixerLayer mixerLayer,
        ulong channelId,
        FilterGraph filterGraph)
    {
        var channel =
            mixerLayer.GroupChannels.First(c => c.ChannelId == channelId);

        var filterChainConfig = new FilterChainModuleConfig()
        {
            CaptureProps = new()
            {
                Name = $"mixer-fc-group-{channelId}-capture",
                Description =
                    $"Capture Node for Mixer Filter Group Channel {channelId}",
                Linger = true,
                AutoConnect = true,
                DontFallback = true,
                Passive = false,
                TargetObject = channel.PreFxLooper.PlaybackNode.ObjectSerial
                    .ToString(),
                MediaClass = "Stream/Input/Audio",
                AudioPosition = ["FL", "FR"]
            },
            PlaybackProps = new()
            {
                Name = $"mixer-fc-group-{channelId}-playback",
                Description =
                    $"Playback Node for Mixer Filter Group Channel {channelId}",
                Linger = true,
                AutoConnect = true,
                DontFallback = true,
                Passive = false,
                TargetObject = channel.OutputLoopback.CaptureNode.ObjectSerial
                    .ToString(),
                MediaClass = "Stream/Output/Audio",
                AudioPosition = ["FL", "FR"]
            },
            FilterGraph = filterGraph.ToFilterGraphConfig()
        };

        var filterChain =
            await Fr.Sonic.FrSonic.ModuleFactory
                .CreateFilterChainAsync(
                    $"mixer-fc-group-{channelId}", filterChainConfig);

        channel.PreFxLooper.PlaybackNode.OverrideTargetObject(
            filterChain.CaptureNode.ObjectSerial.ToString());

        channel.OutputLoopback.CaptureNode.OverrideTargetObject(
            filterChain.PlaybackNode.ObjectSerial.ToString());

        foreach (var send in channel.SendLoopbacks)
        {
            send.CaptureNode.OverrideTargetObject(filterChain.PlaybackNode
                .ObjectSerial.ToString());
        }

        var newChannel = channel with { FilterChain = filterChain };

        var newList = mixerLayer.GroupChannels.Select(c =>
            c.ChannelId == channelId ? newChannel : c).ToList();

        return mixerLayer with { GroupChannels = newList };
    }

    public async Task<MixerLayer> AddFilterToMasterChannel(
        MixerLayer mixerLayer,
        FilterGraph filterGraph)
    {
        var channel = mixerLayer.MasterChannel;

        var filterChainConfig = new FilterChainModuleConfig()
        {
            CaptureProps = new()
            {
                Name = "mixer-fc-master-capture",
                Description = "Capture Node for Mixer Filter Master Channel",
                Linger = true,
                AutoConnect = true,
                DontFallback = true,
                Passive = false,
                TargetObject = channel.PreFxLooper.PlaybackNode.ObjectSerial
                    .ToString(),
                MediaClass = "Stream/Input/Audio",
                AudioPosition = ["FL", "FR"]
            },
            PlaybackProps = new()
            {
                Name = "mixer-fc-master-playback",
                Description = "Playback Node for Mixer Filter Master Channel",
                Linger = true,
                AutoConnect = true,
                DontFallback = true,
                Passive = false,
                TargetObject = channel.OutputLoopback.CaptureNode.ObjectSerial
                    .ToString(),
                MediaClass = "Stream/Output/Audio",
                AudioPosition = ["FL", "FR"]
            },
            FilterGraph = filterGraph.ToFilterGraphConfig()
        };

        var filterChain =
            await Fr.Sonic.FrSonic.ModuleFactory
                .CreateFilterChainAsync("mixer-fc-master", filterChainConfig);

        channel.PreFxLooper.PlaybackNode.OverrideTargetObject(
            filterChain.CaptureNode.ObjectSerial.ToString());

        channel.OutputLoopback.CaptureNode.OverrideTargetObject(
            filterChain.PlaybackNode.ObjectSerial.ToString());

        var newMaster = channel with
        {
            FilterChain = filterChain,
            FilterGraph = filterGraph
        };

        return mixerLayer with { MasterChannel = newMaster };
    }

    public async Task<FilterChain> CreateGlobalMasterFilterChain(
        string physicalOutputSerial)
    {
        return await Fr.Sonic.FrSonic.ModuleFactory
            .CreateFilterChainAsync("global-master", new FilterChainModuleConfig
            {
                CaptureProps = new()
                {
                    Name = "global-master-capture",
                    Description = "Global Master Capture",
                    Linger = true,
                    AutoConnect = false,
                    DontFallback = true,
                    Passive = false,
                    MediaClass = CaptureNodeMediaClass,
                    AudioPosition = ["AUX0", "AUX1", "AUX2", "AUX3"]
                },
                PlaybackProps = new()
                {
                    Name = "global-master-playback",
                    Description = "Global Master Playback",
                    Linger = true,
                    AutoConnect = true,
                    DontFallback = true,
                    Passive = false,
                    TargetObject = physicalOutputSerial,
                    MediaClass = PlaybackNodeMediaClass,
                    AudioPosition = StereoAudioPosition
                },
                FilterGraph = new FilterGraphConfig
                {
                    Nodes =
                    [
                        new FilterGraphNode
                        {
                            Name = "xfade",
                            Type = "lv2",
                            Plugin = "http://gareus.org/oss/lv2/xfade"
                        }
                    ],
                    Links = []
                }
            });
    }

    // The cue filter chain's capture targets the GlobalMaster capture node.
    // In PipeWire a Stream/Input/Audio targeting another Stream/Input/Audio
    // connects to its monitor output ports, giving a pre-xfade tap of each layer:
    // AUX0/1 = Layer A, AUX2/3 = Layer B.
    public async Task<FilterChain> CreateCueFilterChain(
        string globalMasterCaptureSerial, string cueOutputSerial)
    {
        return await Fr.Sonic.FrSonic.ModuleFactory
            .CreateFilterChainAsync("cue", new FilterChainModuleConfig
            {
                CaptureProps = new()
                {
                    Name = "cue-capture",
                    Description = "Cue Capture",
                    Linger = true,
                    AutoConnect = true,
                    DontFallback = true,
                    Passive = false,
                    TargetObject = globalMasterCaptureSerial,
                    MediaClass = CaptureNodeMediaClass,
                    AudioPosition = ["AUX0", "AUX1", "AUX2", "AUX3"]
                },
                PlaybackProps = new()
                {
                    Name = "cue-playback",
                    Description = "Cue Playback",
                    Linger = true,
                    AutoConnect = true,
                    DontFallback = true,
                    Passive = false,
                    TargetObject = cueOutputSerial,
                    MediaClass = PlaybackNodeMediaClass,
                    AudioPosition = StereoAudioPosition
                },
                FilterGraph = new FilterGraphConfig
                {
                    Nodes =
                    [
                        new FilterGraphNode
                        {
                            Name = "xfade",
                            Type = "lv2",
                            Plugin = "http://gareus.org/oss/lv2/xfade"
                        }
                    ],
                    Links = []
                }
            });
    }

    public async Task<MixerLayer> Create(string? defaultMasterName,
        ulong layerId,
        int numberOfChannels,
        int numberOfGroupChannels,
        int numberOfReturnChannels,
        ulong[]? ignoreSerials = null,
        string? globalMasterCaptureSerial = null)
    {
        ignoreSerials ??= [];

        var outputChannels = CreateOutputChannels(ignoreSerials);

        var defaultOutput = defaultMasterName == null
            ? outputChannels.First()
            : outputChannels.FirstOrDefault(c =>
                  c.CaptureNode.Name == defaultMasterName) ??
              outputChannels.First();

        var inputChannels = CreateInputChannels(ignoreSerials);

        var masterChannel = await CreateMasterChannel(layerId, defaultOutput,
            globalMasterCaptureSerial);

        var returns = await CreateReturnChannels(masterChannel, layerId,
            numberOfReturnChannels);

        var groupChannels = await CreateGroupChannels(layerId, masterChannel,
            returns, numberOfGroupChannels, numberOfReturnChannels);

        var channels = await CreateChannels(layerId, masterChannel, returns,
            numberOfChannels, numberOfReturnChannels);

        return new(
            layerId,
            "Mixer",
            masterChannel,
            groupChannels,
            channels,
            returns,
            inputChannels,
            outputChannels);
    }

    private const string CaptureNodeMediaClass = "Stream/Input/Audio";
    private const string PlaybackNodeMediaClass = "Stream/Output/Audio";
    private static readonly List<string> StereoAudioPosition = ["FL", "FR"];

    private static NodePropertiesConfig CaptureBaseProps(bool autoConnect,
        string name,
        string? description = null,
        bool passive = true) => new()
    {
        Linger = true,
        Name = name,
        Description = description ?? name,
        AudioPosition = StereoAudioPosition,
        MediaClass = CaptureNodeMediaClass,
        DontFallback = true,
        AutoConnect = autoConnect,
        Passive = passive
    };

    private static NodePropertiesConfig CaptureBasePropsWithTargetObject(
        bool autoConnect,
        string targetObject,
        string name,
        string? description = null,
        bool passive = true)
    {
        var props = CaptureBaseProps(autoConnect, name, description, passive);
        props.TargetObject = targetObject;
        return props;
    }

    private static NodePropertiesConfig PlaybackBaseProps(bool autoConnect,
        string name,
        string? description = null) => new()
    {
        Linger = true,
        Name = name,
        Description = description ?? name,
        AudioPosition = StereoAudioPosition,
        MediaClass = PlaybackNodeMediaClass,
        DontFallback = true,
        AutoConnect = autoConnect,
        Passive = true
    };

    private static NodePropertiesConfig PlaybackBasePropsWithTargetObject(
        bool autoConnect,
        string targetObject, string name,
        string? description = null)
    {
        var props = PlaybackBaseProps(autoConnect, name, description);
        props.TargetObject = targetObject;
        return props;
    }

    private async Task<MasterChannel> CreateMasterChannel(ulong layerId,
        OutputChannel defaultOutput, string? globalMasterCaptureSerial = null)
    {
        var preFxLooper = await CreateLooper(
            layerId,
            0,
            "master",
            "pre",
            null,
            null);

        // When a GlobalMaster exists the postFx looper routes into it;
        // layer position (0→AUX0/1, 1→AUX2/3) determines which xfade input pair.
        // Without a GlobalMaster it routes straight to the physical output.
        var masterTarget = globalMasterCaptureSerial
            ?? defaultOutput.CaptureNode.ObjectSerial.ToString();
        var playbackAudioPosition = globalMasterCaptureSerial is not null
            ? (layerId == 0 ? "AUX0,AUX1" : "AUX2,AUX3")
            : null;

        var postFxLooper = await CreateLooper(
            layerId,
            0,
            "master",
            "post",
            preFxLooper.PlaybackNode.ObjectSerial.ToString(),
            masterTarget,
            playbackAudioPosition);

        preFxLooper.PlaybackNode.OverrideTargetObject(
            postFxLooper.CaptureNode.ObjectSerial.ToString());

        return new(
            "Master",
            0,
            preFxLooper,
            preFxLooper,
            null,
            null,
            postFxLooper,
            defaultOutput.CaptureNode);
    }

    private async Task<List<GroupChannel>> CreateGroupChannels(ulong layerId,
        MasterChannel masterChannel, List<ReturnChannel> returnChannels,
        int numberOfGroupChannels, int numberOfReturnChannels) =>
        (await Task.WhenAll(Enumerable.Range(1, numberOfGroupChannels)
            .Select(i =>
                CreateGroupChannel(i, layerId, masterChannel, returnChannels,
                    numberOfGroupChannels, numberOfReturnChannels))))
        .ToList();

    private async Task<GroupChannel> CreateGroupChannel(int index,
        ulong layerId,
        MasterChannel masterChannel, List<ReturnChannel> returnChannels,
        int numberOfGroupChannels, int numberOfReturnChannels)
    {
        var channelId =
            (ulong)((index - 1) + numberOfGroupChannels * (int)layerId);

        var preFxLooper = await CreateLooper(
            layerId,
            channelId,
            "group",
            "pre",
            null,
            null);

        var postFxLooper = await CreateLooper(
            layerId,
            channelId,
            "group",
            "post",
            preFxLooper.PlaybackNode.ObjectSerial.ToString(),
            masterChannel.InputLoopback.CaptureNode.ObjectSerial.ToString());

        preFxLooper.PlaybackNode.OverrideTargetObject(
            postFxLooper.CaptureNode.ObjectSerial.ToString());

        var sendLoopbacks = (await Task.WhenAll(Enumerable
            .Range(1, numberOfReturnChannels)
            .Select(i =>
                wireplumberService.CreateLoopbackModule(
                    $"send-loopback-group-{index}-send-{i}", new()
                    {
                        CaptureProps = CaptureBasePropsWithTargetObject(true,
                            preFxLooper.PlaybackNode.ObjectSerial
                                .ToString(),
                            $"group-{index}-send-{i}-loopback-capture-{layerId}"),
                        PlaybackProps = PlaybackBasePropsWithTargetObject(true,
                            returnChannels[i - 1].InputLoopback
                                .CaptureNode.ObjectSerial.ToString(),
                            $"group-{index}-send-{i}-loopback-playback-{layerId}"),
                    })))).ToList();

        var id = index - 1 + (int)layerId * numberOfGroupChannels;

        var silenceHandle = Fr.Sonic.FrSonic.CreateSilenceProducer(
            preFxLooper.CaptureNode.ObjectSerial);

        return new(
            $"Group {index}",
            (ulong)id,
            preFxLooper,
            preFxLooper,
            null,
            postFxLooper,
            sendLoopbacks,
            masterChannel.InputLoopback.CaptureNode,
            silenceHandle);
    }

    private List<OutputChannel> CreateOutputChannels(ulong[] ignoreSerials)
    {
        var captureNodes = wireplumberService.GetCaptureNodes();
        return captureNodes.Where(n => !ignoreSerials.Contains(n.ObjectSerial))
            .Select(CreateOutputChannel).ToList();
    }

    private static OutputChannel CreateOutputChannel(Node captureNode) =>
        new OutputChannel(captureNode.Description ?? "Unknown", captureNode);

    private List<InputChannel> CreateInputChannels(ulong[] ignoreSerials)
    {
        var playbackNodes = wireplumberService.GetPlaybackNodes();
        return playbackNodes.Where(n => !ignoreSerials.Contains(n.ObjectSerial))
            .Select(CreateInputChannel).ToList();
    }

    private static InputChannel CreateInputChannel(Node playbackNode) =>
        new InputChannel(playbackNode.Description ?? "Unknown", playbackNode);

    private async Task<List<ReturnChannel>> CreateReturnChannels(
        MasterChannel master, ulong layerId, int numberOfReturnChannels) =>
        (await Task.WhenAll(
            Enumerable.Range(1, numberOfReturnChannels)
                .Select(i => CreateReturnChannel(i, master, layerId))))
        .ToList();

    private async Task<ReturnChannel> CreateReturnChannel(int sendId,
        MasterChannel master, ulong layerId)
    {
        var inputLoopback = await wireplumberService.CreateLoopbackModule(
            $"return-input-loopback-{sendId}", new()
            {
                CaptureProps = CaptureBaseProps(false,
                    $"return-{sendId}-input-loopback-capture-{layerId}"),
                PlaybackProps = PlaybackBaseProps(false,
                    $"return-{sendId}-input-loopback-playback-{layerId}"),
            });

        var outputLoopback = await wireplumberService.CreateLoopbackModule(
            $"return-output-loopback-{sendId}", new()
            {
                CaptureProps = CaptureBasePropsWithTargetObject(true,
                    inputLoopback.PlaybackNode.ObjectSerial.ToString(),
                    $"return-{sendId}-output-loopback-capture-{layerId}"),
                PlaybackProps = PlaybackBasePropsWithTargetObject(true,
                    master.InputLoopback.CaptureNode.ObjectSerial
                        .ToString(),
                    $"return-{sendId}-output-loopback-playback-{layerId}")
            });

        return new(
            $"Return {sendId}",
            inputLoopback,
            null,
            outputLoopback);
    }

    private async Task<List<ChannelStrip>> CreateChannels(ulong layerId,
        MasterChannel master, List<ReturnChannel> returnChannels,
        int numberOfChannels, int numberOfReturnChannels)
    {
        var channels = new List<ChannelStrip>();
        foreach (var channelId in Enumerable.Range(1, numberOfChannels))
        {
            var strip = await CreateChannelStrip(
                layerId,
                $"Channel {channelId}",
                (ulong)channelId,
                returnChannels, master, numberOfChannels, numberOfReturnChannels);
            channels.Add(strip);
        }

        return channels;
    }

    private async Task<ChannelStrip> CreateChannelStrip(ulong layerId,
        string name, ulong channelId, List<ReturnChannel> returnChannels,
        MasterChannel master, int numberOfChannels, int numberOfReturnChannels)
    {
        var preFxLooper = await CreateLooper(
            layerId,
            channelId,
            "channel",
            "pre",
            0.ToString(),
            null);

        var postFxLooper = await CreateLooper(
            layerId,
            channelId,
            "channel",
            "post",
            preFxLooper.PlaybackNode.ObjectSerial.ToString(),
            master.InputLoopback.CaptureNode.ObjectSerial.ToString());

        preFxLooper.PlaybackNode.OverrideTargetObject(
            postFxLooper.CaptureNode.ObjectSerial.ToString());

        var sendLoopbacks = await Task.WhenAll(Enumerable
            .Range(1, numberOfReturnChannels)
            .Select(i =>
                wireplumberService.CreateLoopbackModule(
                    $"send-loopback-{channelId}-send-{i}", new()
                    {
                        CaptureProps = CaptureBasePropsWithTargetObject(true,
                            preFxLooper.PlaybackNode.ObjectSerial
                                .ToString(),
                            $"channel-{channelId}-send-{i}-loopback-capture-{layerId}"),
                        PlaybackProps = PlaybackBasePropsWithTargetObject(true,
                            returnChannels[i - 1].InputLoopback
                                .CaptureNode.ObjectSerial.ToString(),
                            $"channel-{channelId}-send-{i}-loopback-playback-{layerId}"),
                    })));

        var id = (channelId - 1) + layerId * (ulong)numberOfChannels;

        var silenceHandle = Fr.Sonic.FrSonic.CreateSilenceProducer(
            preFxLooper.CaptureNode.ObjectSerial);

        return new(
            name,
            id,
            preFxLooper,
            preFxLooper,
            null,
            null,
            postFxLooper,
            sendLoopbacks.ToList(),
            null,
            master.InputLoopback.CaptureNode,
            silenceHandle);
    }

    private static async Task<Looper> CreateLooper(ulong layerId,
        ulong channelId,
        string ownerKind,
        string position,
        string? captureTargetObject,
        string? playbackTargetObject,
        string? playbackAudioPosition = null)
    {
        var name = $"mixer-{ownerKind}-{channelId}-{position}-looper-{layerId}";
        var archiveFolder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "SonicEddy",
            "loop_archives",
            $"looper_{ownerKind}_{channelId}_{position}");

        return await Fr.Sonic.FrSonic.LooperFactory.CreateLooperAsync(
            new LooperConfig(
                name,
                $"Mixer {ownerKind} {channelId} {position}-FX looper",
                captureTargetObject,
                playbackTargetObject,
                archiveFolder,
                Mix: 0.0f,
                PlaybackAudioPosition: playbackAudioPosition));
    }
}
