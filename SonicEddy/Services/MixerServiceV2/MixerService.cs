using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fr.Wireplumber.Model.Objects;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Wireplumber;

namespace SonicEddy.Services.MixerServiceV2;

public class MixerService(
    IAppDataService appDataService,
    IWireplumberService wireplumberService) : IMixerService
{
    private const int InitialChannelCount = 1;
    private const int SendChannelCount = 1;
    private string? _masterOutputName;

    public Mixer? CurrentMixer { get; private set; }

    public async Task<Mixer> NewCurrentMixer(string name)
    {
        CurrentMixer = await Create();
        return CurrentMixer;
    }

    public Task<ChannelStrip> AddFilterToChannelStrip(ulong channelId,
        FilterGraph filterGraph)
    {
        throw new NotImplementedException();
    }

    public Task<Guid> PersistCurrentMixer()
    {
        throw new NotImplementedException();
    }

    public Task<Mixer> RestoreMixer(Guid id)
    {
        throw new NotImplementedException();
    }

    public void DeleteMixer(Guid id)
    {
        throw new NotImplementedException();
    }

    private async Task<Mixer> Create()
    {
        var outputChannels = CreateOutputChannels();

        if (_masterOutputName is null)
            outputChannels.First().IsMaster = true;

        var inputChannels = CreateInputChannels();

        var master = outputChannels.First(c => c.IsMaster);

        var returns = await CreateReturnChannels(master);

        var channels = await CreateChannels(returns, master);

        return new Mixer(
            "Mixer",
            channels,
            returns,
            inputChannels,
            outputChannels);
    }

    private const string CaptureNodeMediaClass = "Stream/Input/Audio";
    private const string PlaybackNodeMediaClass = "Stream/Output/Audio";
    private static readonly List<string> StereoAudioPosition = ["FL", "FR"];

    private List<OutputChannel> CreateOutputChannels()
    {
        var captureNodes = wireplumberService.GetCaptureNodes();
        return captureNodes.Select(CreateOutputChannel).ToList();
    }

    private static OutputChannel CreateOutputChannel(Node captureNode) =>
        new OutputChannel(captureNode.Name ?? "Unknown", captureNode);

    private List<InputChannel> CreateInputChannels()
    {
        var playbackNodes = wireplumberService.GetPlaybackNodes();
        return playbackNodes.Select(CreateInputChannel).ToList();
    }

    private static InputChannel CreateInputChannel(Node playbackNode) =>
        new InputChannel(playbackNode.Name ?? "Unknown", playbackNode);

    private async Task<List<ReturnChannel>> CreateReturnChannels(
        OutputChannel master) =>
        (await Task.WhenAll(
            Enumerable.Range(1, SendChannelCount)
                .Select(i => CreateReturnChannel(i, master))))
        .ToList();

    private async Task<ReturnChannel> CreateReturnChannel(int sendId,
        OutputChannel master)
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
                    TargetObject = master.CaptureNode.ObjectSerial.ToString()
                }
            });

        return new(
            $"Return {sendId}",
            inputLoopback,
            null,
            outputLoopback,
            master);
    }

    private async Task<List<ChannelStrip>> CreateChannels(
        List<ReturnChannel> returnChannels, OutputChannel master)
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
        OutputChannel master)
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
                    TargetObject = master.CaptureNode.ObjectSerial.ToString()
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
            null);
    }
}