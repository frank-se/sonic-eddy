using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fr.Wireplumber.Model.Config.FilterChain;
using Fr.Wireplumber.Model.Objects;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Conversions;
using SonicEddy.Services.Wireplumber;

namespace SonicEddy.Services.MixerServiceV2;

public class MixerEditor(IWireplumberService wireplumberService)
{
    private const int InitialChannelCount = 1;
    private const int SendChannelCount = 1;

    public async Task<Mixer> AddFilterToChannelStrip(
        Mixer mixer,
        ulong channelId,
        FilterGraph filterGraph)
    {
        var channel =
            mixer.Channels.First(c => c.ChannelId == channelId);

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
            await Fr.Wireplumber.Wireplumber.ModuleFactory
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
            FilterChain = filterChain
        };

        var newList = mixer.Channels.Select(c =>
            c.ChannelId == channelId ? newChannel : c).ToList();

        return mixer with
        {
            Channels = newList
        };
    }

    public async Task<Mixer> Create(string? defaultMasterName)
    {
        var outputChannels = CreateOutputChannels();

        var defaultOutput = defaultMasterName == null
            ? outputChannels.First()
            : outputChannels.FirstOrDefault(c =>
                  c.CaptureNode.Name == defaultMasterName) ??
              outputChannels.First();

        var inputChannels = CreateInputChannels();

        var masterChannel = await CreateMasterChannel(defaultOutput);

        var returns = await CreateReturnChannels(masterChannel);

        var groupChannels = await CreateGroupChannels(masterChannel, returns);

        var channels = await CreateChannels(masterChannel, returns);

        return new Mixer(
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

    private async Task<MasterChannel> CreateMasterChannel(
        OutputChannel defaultOutput)
    {
        var inputLoopback = await wireplumberService.CreateLoopbackModule(
            "input-loopback-master", new()
            {
                CaptureProps = new()
                {
                    Linger = true,
                    Name = "master-input-loopback-capture",
                    Description = $"master-input-loopback-capture",
                    MediaClass = CaptureNodeMediaClass,
                    DontFallback = true,
                    AutoConnect = false
                },
                PlaybackProps = new()
                {
                    Linger = true,
                    Name = "master-input-loopback-playback",
                    Description = "master-input-loopback-playback",
                    AudioPosition = StereoAudioPosition,
                    MediaClass = PlaybackNodeMediaClass,
                    DontFallback = true,
                    AutoConnect = false
                }
            });

        var outputLoopback = await wireplumberService.CreateLoopbackModule(
            $"output-loopback-master", new()
            {
                CaptureProps = new()
                {
                    Linger = true,
                    Name = "master-output-loopback-capture",
                    Description =
                        "master-output-loopback-capture",
                    DontFallback = true,
                    MediaClass = CaptureNodeMediaClass,
                    AutoConnect = true,
                    TargetObject =
                        inputLoopback.PlaybackNode.ObjectSerial.ToString(),
                },
                PlaybackProps = new()
                {
                    Linger = true,
                    Name = "master-output-loopback-playback",
                    Description = "master-output-loopback-playback",
                    AudioPosition = StereoAudioPosition,
                    MediaClass = PlaybackNodeMediaClass,
                    DontFallback = true,
                    AutoConnect = true,
                    TargetObject =
                        defaultOutput.CaptureNode.ObjectSerial.ToString()
                }
            });

        return new(
            "Master",
            1,
            inputLoopback,
            null,
            outputLoopback,
            defaultOutput.CaptureNode);
    }

    private async Task<List<GroupChannel>> CreateGroupChannels(
        MasterChannel masterChannel, List<ReturnChannel> returnChannels) =>
        (await Task.WhenAll(Enumerable.Range(1, 4)
            .Select(i => CreateGroupChannel(i, masterChannel, returnChannels))))
        .ToList();

    private async Task<GroupChannel> CreateGroupChannel(int index,
        MasterChannel masterChannel, List<ReturnChannel> returnChannels)
    {
        var inputLoopback = await wireplumberService.CreateLoopbackModule(
            $"input-loopback-group-{index}", new()
            {
                CaptureProps = new()
                {
                    Linger = true,
                    Name = $"group-{index}-input-loopback-capture",
                    Description = $"group-{index}-input-loopback-capture",
                    MediaClass = CaptureNodeMediaClass,
                    DontFallback = true,
                    AutoConnect = false
                },
                PlaybackProps = new()
                {
                    Linger = true,
                    Name = $"group-{index}-input-loopback-playback",
                    Description = $"group-{index}-input-loopback-playback",
                    AudioPosition = StereoAudioPosition,
                    MediaClass = PlaybackNodeMediaClass,
                    DontFallback = true,
                    AutoConnect = false
                }
            });

        var outputLoopback = await wireplumberService.CreateLoopbackModule(
            $"output-loopback-master", new()
            {
                CaptureProps = new()
                {
                    Linger = true,
                    Name = $"group-{index}-output-loopback-capture",
                    Description = $"group-{index}-output-loopback-capture",
                    DontFallback = true,
                    MediaClass = CaptureNodeMediaClass,
                    AutoConnect = true,
                    TargetObject =
                        inputLoopback.PlaybackNode.ObjectSerial.ToString(),
                },
                PlaybackProps = new()
                {
                    Linger = true,
                    Name = $"group-{index}-output-loopback-playback",
                    Description = $"group-{index}-output-loopback-playback",
                    AudioPosition = StereoAudioPosition,
                    MediaClass = PlaybackNodeMediaClass,
                    DontFallback = true,
                    AutoConnect = true,
                    TargetObject = masterChannel.InputLoopback.CaptureNode
                        .ObjectSerial.ToString()
                }
            });

        var sendLoopbacks = (await Task.WhenAll(Enumerable
            .Range(1, SendChannelCount)
            .Select(i =>
                wireplumberService.CreateLoopbackModule(
                    $"send-loopback-group-{index}-send-{i}", new()
                    {
                        CaptureProps = new()
                        {
                            Linger = true,
                            Name =
                                $"group-{index}-send-{i}-loopback-capture",
                            Description =
                                $"group-{index}-send-{i}-loopback-capture",
                            MediaClass = CaptureNodeMediaClass,
                            TargetObject =
                                inputLoopback.PlaybackNode.ObjectSerial
                                    .ToString(),
                            DontFallback = true,
                        },
                        PlaybackProps = new()
                        {
                            Linger = true,
                            Name =
                                $"group-{index}-send-{i}-loopback-playback",
                            Description =
                                $"group-{index}-send-{i}-loopback-playback",
                            AudioPosition = StereoAudioPosition,
                            MediaClass = PlaybackNodeMediaClass,
                            TargetObject = returnChannels[i - 1].InputLoopback
                                .CaptureNode.ObjectSerial.ToString(),
                            DontFallback = true,
                        }
                    })))).ToList();

        return new(
            $"Group {index}",
            (ulong)index,
            inputLoopback,
            null,
            outputLoopback,
            sendLoopbacks,
            masterChannel.InputLoopback.CaptureNode);
    }

    private List<OutputChannel> CreateOutputChannels()
    {
        var captureNodes = wireplumberService.GetCaptureNodes();
        return captureNodes.Select(CreateOutputChannel).ToList();
    }

    private static OutputChannel CreateOutputChannel(Node captureNode) =>
        new OutputChannel(captureNode.Description ?? "Unknown", captureNode);

    private List<InputChannel> CreateInputChannels()
    {
        var playbackNodes = wireplumberService.GetPlaybackNodes();
        return playbackNodes.Select(CreateInputChannel).ToList();
    }

    private static InputChannel CreateInputChannel(Node playbackNode) =>
        new InputChannel(playbackNode.Description ?? "Unknown", playbackNode);

    private async Task<List<ReturnChannel>> CreateReturnChannels(
        MasterChannel master) =>
        (await Task.WhenAll(
            Enumerable.Range(1, SendChannelCount)
                .Select(i => CreateReturnChannel(i, master))))
        .ToList();

    private async Task<ReturnChannel> CreateReturnChannel(int sendId,
        MasterChannel master)
    {
        var inputLoopback = await wireplumberService.CreateLoopbackModule(
            $"return-input-loopback-{sendId}", new()
            {
                CaptureProps = new()
                {
                    Linger = true,
                    Name = $"return-{sendId}-input-loopback-capture",
                    Description = $"return-{sendId}-input-loopback-capture",
                    MediaClass = CaptureNodeMediaClass,
                    AutoConnect = false
                },
                PlaybackProps = new()
                {
                    Linger = true,
                    Name = $"return-{sendId}-input-loopback-playback",
                    Description =
                        $"return-{sendId}-input-loopback-playback",
                    AudioPosition = StereoAudioPosition,
                    MediaClass = PlaybackNodeMediaClass,
                    AutoConnect = false,
                    DontFallback = true,
                }
            });

        var outputLoopback = await wireplumberService.CreateLoopbackModule(
            $"return-output-loopback-{sendId}", new()
            {
                CaptureProps = new()
                {
                    Linger = true,
                    Name = $"return-{sendId}-output-loopback-capture",
                    Description =
                        $"return-{sendId}-output-loopback-capture",
                    MediaClass = CaptureNodeMediaClass,
                    TargetObject =
                        inputLoopback.PlaybackNode.ObjectSerial.ToString(),
                    AutoConnect = true,
                    DontFallback = true
                },
                PlaybackProps = new()
                {
                    Linger = true,
                    Name = $"return-{sendId}-output-loopback-playback",
                    Description =
                        $"return-{sendId}-output-loopback-playback",
                    AudioPosition = StereoAudioPosition,
                    MediaClass = PlaybackNodeMediaClass,
                    AutoConnect = true,
                    DontFallback = true,
                    TargetObject = master.InputLoopback.CaptureNode.ObjectSerial
                        .ToString()
                }
            });

        return new(
            $"Return {sendId}",
            inputLoopback,
            null,
            outputLoopback);
    }

    private async Task<List<ChannelStrip>> CreateChannels(
        MasterChannel master, List<ReturnChannel> returnChannels)
    {
        var channelIds = Enumerable.Range(1, InitialChannelCount);
        var channels = new List<ChannelStrip>();
        foreach (var channelId in channelIds)
        {
            var strip = await CreateChannelStrip(
                $"Channel {channelId}",
                (ulong)channelId,
                returnChannels, master);
            channels.Add(strip);
        }

        return channels;
    }

    private async Task<ChannelStrip> CreateChannelStrip(
        string name, ulong channelId, List<ReturnChannel> returnChannels,
        MasterChannel master)
    {
        var inputLoopback = await wireplumberService.CreateLoopbackModule(
            $"input-loopback-{channelId}", new()
            {
                CaptureProps = new()
                {
                    Linger = true,
                    Name = $"channel-{channelId}-input-loopback-capture",
                    Description = $"channel-{channelId}-input-loopback-capture",
                    MediaClass = CaptureNodeMediaClass,
                    DontFallback = true,
                    TargetObject = 0.ToString()
                },
                PlaybackProps = new()
                {
                    Linger = true,
                    Name = $"channel-{channelId}-input-loopback-playback",
                    Description =
                        $"channel-{channelId}-input-loopback-playback",
                    AudioPosition = StereoAudioPosition,
                    MediaClass = PlaybackNodeMediaClass,
                    DontFallback = true,
                    AutoConnect = false
                }
            });

        var outputLoopback = await wireplumberService.CreateLoopbackModule(
            $"output-loopback-{channelId}", new()
            {
                CaptureProps = new()
                {
                    Linger = true,
                    Name = $"channel-{channelId}-output-loopback-capture",
                    Description =
                        $"channel-{channelId}-output-loopback-capture",
                    DontFallback = true,
                    MediaClass = CaptureNodeMediaClass,
                    TargetObject =
                        inputLoopback.PlaybackNode.ObjectSerial.ToString(),
                },
                PlaybackProps = new()
                {
                    Linger = true,
                    Name = $"channel-{channelId}-output-loopback-playback",
                    Description =
                        $"channel-{channelId}-output-loopback-playback",
                    AudioPosition = StereoAudioPosition,
                    MediaClass = PlaybackNodeMediaClass,
                    DontFallback = true,
                    AutoConnect = true,
                    TargetObject = master.InputLoopback.CaptureNode.ObjectSerial
                        .ToString()
                }
            });

        var sendLoopbacks = await Task.WhenAll(Enumerable
            .Range(1, SendChannelCount)
            .Select(i =>
                wireplumberService.CreateLoopbackModule(
                    $"send-loopback-{channelId}-send-{i}", new()
                    {
                        CaptureProps = new()
                        {
                            Linger = true,
                            Name =
                                $"channel-{channelId}-send-{i}-loopback-capture",
                            Description =
                                $"channel-{channelId}-send-{i}-loopback-capture",
                            MediaClass = CaptureNodeMediaClass,
                            TargetObject =
                                inputLoopback.PlaybackNode.ObjectSerial
                                    .ToString(),
                            DontFallback = true,
                        },
                        PlaybackProps = new()
                        {
                            Linger = true,
                            Name =
                                $"channel-{channelId}-send-{i}-loopback-playback",
                            Description =
                                $"channel-{channelId}-send-{i}-loopback-playback",
                            AudioPosition = StereoAudioPosition,
                            MediaClass = PlaybackNodeMediaClass,
                            TargetObject = returnChannels[i - 1].InputLoopback
                                .CaptureNode.ObjectSerial.ToString(),
                            DontFallback = true,
                        }
                    })));

        return new(
            name,
            channelId,
            inputLoopback,
            null,
            outputLoopback,
            sendLoopbacks.ToList(),
            null,
            master.InputLoopback.CaptureNode);
    }
}