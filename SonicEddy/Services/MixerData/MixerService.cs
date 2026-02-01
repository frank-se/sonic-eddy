using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fr.Wireplumber.Model.Config.FilterChain;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Model.Params;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Contracts.Parameters;
using SonicEddy.Conversions;
using SonicEddy.Services.AppData;

namespace SonicEddy.Services.MixerData;

public class MixerService(IAppDataService appDataService) : IMixerService
{
    private readonly IAppDataService _appDataService = appDataService;
    private Mixer _mixer = NewMixer("sonic-eddy");
    private Guid _currentMixerId = Guid.NewGuid();
    private bool _persisted = false;

    public Mixer CurrentMixer => _mixer;

    public Mixer NewCurrentMixer(string name)
    {
        DeleteCurrentMixer();
        _mixer = NewMixer(name);
        return _mixer;
    }

    public async Task<Mixer> AddChannelStripToCurrentMixer(string name,
        Node inputNode)
    {
        var currentMaxChannelId = _mixer.ChannelStrips.Count > 0
            ? _mixer.ChannelStrips.Select(c => c.ChannelId).Max()
            : 0;

        var channelId = currentMaxChannelId + 1;

        var loopbackModule = await Fr.Wireplumber.Wireplumber.ModuleFactory
            .CreateLoopbackModuleAsync($"channel-{channelId}-loopback",
                new()
                {
                    CaptureProps = new()
                    {
                        Name = $"channel-{channelId}-loopback-capture",
                        Description = $"channel-{channelId}-loopback-capture",
                        AutoConnect = true,
                        TargetObject = inputNode.ObjectSerial.ToString(),
                        MediaClass = "Stream/Input/Audio"
                    },
                    PlaybackProps = new()
                    {
                        Name = $"channel-{channelId}-loopback-playback",
                        Description = $"channel-{channelId}-loopback-playback",
                        AudioPosition = ["FL", "FR"],
                        AutoConnect = true,
                        MediaClass = "Stream/Output/Audio"
                    }
                });

        _mixer = _mixer with
        {
            ChannelStrips =
            [
                .._mixer.ChannelStrips,
                new(name, channelId, inputNode, null, loopbackModule)
            ]
        };

        return _mixer;
    }

    public async Task<ChannelStrip> AddFilterToChannelStrip(ulong channelId,
        FilterGraph filterGraph)
    {
        var channel = _mixer.ChannelStrips.First(c => c.ChannelId == channelId);

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
                TargetObject = channel.InputNode.ObjectSerial.ToString(),
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
                TargetObject = channel.LoopbackModule.CaptureNode.ObjectSerial
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

        var newChannel = channel with
        {
            FilterModule = filterChain
        };

        var newList = _mixer.ChannelStrips.Select(c =>
            c.ChannelId == channelId ? newChannel : c).ToList();

        _mixer = _mixer with
        {
            ChannelStrips = new(newList)
        };

        return newChannel;
    }

    public async Task<Guid> PersistCurrentMixer()
    {
        if (_persisted)
        {
            throw new NotImplementedException();
        }

        await _appDataService.CreateMixer(ToStorageMixer());
        _persisted = true;
        return _currentMixerId;
    }

    private Contracts.Mixers.Mixer ToStorageMixer()
    {
        var channels = _mixer.ChannelStrips.Select(ToStorageChannelStrip)
            .ToList();

        return new(
            _currentMixerId,
            _mixer.Name,
            channels);
    }

    private static Contracts.Mixers.ChannelStrip ToStorageChannelStrip(
        ChannelStrip channelStrip)
    {
        var propertiesTask =
            channelStrip.LoopbackModule.PlaybackNode.Properties;
        var channelVolumes = propertiesTask.IsCompleted switch
        {
            false => [],
            true => propertiesTask.Result?.Channels.Select(c => c.Volume)
                .ToList() ?? []
        };

        var parametersTask = channelStrip.FilterModule?.CaptureNode.Params;
        var parameters = parametersTask?.IsCompleted switch
        {
            true => parametersTask.Result?.Values.Select(ToStorageParameter)
                .ToList() ?? [],
            _ => []
        };

        return new(
            channelStrip.ChannelId,
            channelStrip.Name,
            channelStrip.InputNode.Name!,
            channelVolumes,
            Guid.Empty,
            parameters);
    }

    private static ParameterBase ToStorageParameter(
        IParameter parameter) => parameter switch
    {
        Parameter<float> p => new FloatParameter(p.Name, p.Value),
        Parameter<string> p => new StringParameter(p.Name, p.Value),
        Parameter<long> p => new LongParameter(p.Name, p.Value),
        Parameter<int> p => new IntParameter(p.Name, p.Value),
        Parameter<double> p => new DoubleParameter(p.Name, p.Value),
        Parameter<bool> p => new BoolParameter(p.Name, p.Value),
        _ => throw new NotImplementedException()
    };

    public Task<List<Contracts.Mixers.Mixer>> GetAllMixers() =>
        _appDataService.GetAllMixers();

    public async Task<Mixer> RestoreMixer(Guid id)
    {
        var storageMixer = await _appDataService.GetMixer(id);

        NewCurrentMixer(storageMixer.Name);

        foreach (var storageStrip in storageMixer.ChannelStrips)
        {
            var node = Fr.Wireplumber.Wireplumber.NodeRegistry.Objects
                .FirstOrDefault(n => n.Name == storageStrip.InputNodeName);

            if (node is null) continue;

            await AddChannelStripToCurrentMixer(storageStrip.Name, node);

            var channelStrip = _mixer.ChannelStrips.Last();

            if (storageStrip.FilterGraphId is null) continue;

            var filterGraph =
                await _appDataService.GetFilterGraph(
                    (Guid)storageStrip.FilterGraphId!);

            await AddFilterToChannelStrip(channelStrip.ChannelId, filterGraph);
        }

        return _mixer;
    }

    public void DeleteMixer(Guid id) => _appDataService.DeleteMixer(id);

    private void DeleteCurrentMixer()
    {
        foreach (var channelStrip in _mixer.ChannelStrips)
        {
            channelStrip.FilterModule?.Destroy();
            channelStrip.LoopbackModule.Destroy();
        }
    }

    private static Mixer NewMixer(string name) => new Mixer(name, []);
}