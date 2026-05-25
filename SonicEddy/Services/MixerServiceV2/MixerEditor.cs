using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fr.Sonic.PInvoke;
using Fr.Sonic.Model.Config;
using Fr.Sonic.Model.Config.FilterChain;
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
                TargetObject = channel.InputLoopback.PlaybackNode.ObjectSerial
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

    public async Task<MixerLayer> Create(string? defaultMasterName,
        ulong layerId,
        int numberOfChannels,
        int numberOfGroupChannels,
        int numberOfReturnChannels,
        ulong[]? ignoreSerials = null)
    {
        ignoreSerials ??= [];

        var outputChannels = CreateOutputChannels(ignoreSerials);

        var defaultOutput = defaultMasterName == null
            ? outputChannels.First()
            : outputChannels.FirstOrDefault(c =>
                  c.CaptureNode.Name == defaultMasterName) ??
              outputChannels.First();

        var inputChannels = CreateInputChannels(ignoreSerials);

        var masterChannel = await CreateMasterChannel(layerId, defaultOutput);

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
        OutputChannel defaultOutput)
    {
        var inputLoopback = await wireplumberService.CreateLoopbackModule(
            "input-loopback-master", new()
            {
                CaptureProps =
                    CaptureBaseProps(false,
                        $"master-input-loopback-capture-{layerId}"),
                PlaybackProps =
                    PlaybackBaseProps(false,
                        $"master-input-loopback-playback-{layerId}"),
            });

        var outputLoopback = await wireplumberService.CreateLoopbackModule(
            $"output-loopback-master", new()
            {
                CaptureProps = CaptureBasePropsWithTargetObject(true,
                    inputLoopback.PlaybackNode.ObjectSerial.ToString(),
                    $"master-output-loopback-capture-{layerId}"),
                PlaybackProps = PlaybackBasePropsWithTargetObject(true,
                    defaultOutput.CaptureNode.ObjectSerial.ToString(),
                    $"master-output-loopback-playback-{layerId}"),
            });

        return new(
            "Master",
            0,
            inputLoopback,
            null,
            null,
            outputLoopback,
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
        var inputLoopback = await wireplumberService.CreateLoopbackModule(
            $"input-loopback-group-{index}", new()
            {
                CaptureProps = CaptureBaseProps(false,
                    $"group-{index}-input-loopback-capture-{layerId}",
                    passive: false),
                PlaybackProps = PlaybackBaseProps(false,
                    $"group-{index}-input-loopback-playback-{layerId}"),
            });

        var outputLoopback = await wireplumberService.CreateLoopbackModule(
            $"output-loopback-master", new()
            {
                CaptureProps = CaptureBasePropsWithTargetObject(true,
                    inputLoopback.PlaybackNode.ObjectSerial.ToString(),
                    $"group-{index}-output-loopback-capture-{layerId}"),
                PlaybackProps = PlaybackBasePropsWithTargetObject(true,
                    masterChannel.InputLoopback.CaptureNode
                        .ObjectSerial.ToString(),
                    $"group-{index}-output-loopback-playback-{layerId}"),
            });

        var channelId =
            (ulong)((index - 1) + numberOfGroupChannels * (int)layerId);

        var sendLoopbacks = (await Task.WhenAll(Enumerable
            .Range(1, numberOfReturnChannels)
            .Select(i =>
                wireplumberService.CreateLoopbackModule(
                    $"send-loopback-group-{index}-send-{i}", new()
                    {
                        CaptureProps = CaptureBasePropsWithTargetObject(true,
                            inputLoopback.PlaybackNode.ObjectSerial
                                .ToString(),
                            $"group-{index}-send-{i}-loopback-capture-{layerId}"),
                        PlaybackProps = PlaybackBasePropsWithTargetObject(true,
                            returnChannels[i - 1].InputLoopback
                                .CaptureNode.ObjectSerial.ToString(),
                            $"group-{index}-send-{i}-loopback-playback-{layerId}"),
                    })))).ToList();

        var id = index - 1 + (int)layerId * numberOfGroupChannels;

        var silenceHandle = Fr.Sonic.FrSonic.CreateSilenceProducer(
            inputLoopback.CaptureNode.ObjectSerial);

        return new(
            $"Group {index}",
            (ulong)id,
            inputLoopback,
            null,
            outputLoopback,
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
        var inputLoopback = await wireplumberService.CreateLoopbackModule(
            $"input-loopback-{channelId}", new()
            {
                CaptureProps = CaptureBasePropsWithTargetObject(true,
                    0.ToString(),
                    $"channel-{channelId}-input-loopback-capture-{layerId}",
                    passive: false),
                PlaybackProps = PlaybackBaseProps(false,
                    $"channel-{channelId}-input-loopback-playback-{layerId}"),
            });

        var outputLoopback = await wireplumberService.CreateLoopbackModule(
            $"output-loopback-{channelId}", new()
            {
                CaptureProps = CaptureBasePropsWithTargetObject(true,
                    inputLoopback.PlaybackNode.ObjectSerial.ToString(),
                    $"channel-{channelId}-output-loopback-capture-{layerId}"),
                PlaybackProps = PlaybackBasePropsWithTargetObject(true,
                    master.InputLoopback.CaptureNode.ObjectSerial
                        .ToString(),
                    $"channel-{channelId}-output-loopback-playback-{layerId}"),
            });

        var sendLoopbacks = await Task.WhenAll(Enumerable
            .Range(1, numberOfReturnChannels)
            .Select(i =>
                wireplumberService.CreateLoopbackModule(
                    $"send-loopback-{channelId}-send-{i}", new()
                    {
                        CaptureProps = CaptureBasePropsWithTargetObject(true,
                            inputLoopback.PlaybackNode.ObjectSerial
                                .ToString(),
                            $"channel-{channelId}-send-{i}-loopback-capture-{layerId}"),
                        PlaybackProps = PlaybackBasePropsWithTargetObject(true,
                            returnChannels[i - 1].InputLoopback
                                .CaptureNode.ObjectSerial.ToString(),
                            $"channel-{channelId}-send-{i}-loopback-playback-{layerId}"),
                    })));

        var id = (channelId - 1) + layerId * (ulong)numberOfChannels;

        var silenceHandle = Fr.Sonic.FrSonic.CreateSilenceProducer(
            inputLoopback.CaptureNode.ObjectSerial);

        return new(
            name,
            id,
            inputLoopback,
            null,
            null,
            outputLoopback,
            sendLoopbacks.ToList(),
            null,
            master.InputLoopback.CaptureNode,
            silenceHandle);
    }
}