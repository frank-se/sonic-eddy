using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fr.Sonic.PInvoke;
using Fr.Sonic.Model.Config;
using Fr.Sonic.Model.Config.FilterChain;
using Fr.Sonic.Model.Config.Ducker;
using Fr.Sonic.Model.Config.Looper;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Conversions;
using SonicEddy.Services.Midi;
using SonicEddy.Services.ExternalEffects;
using SonicEddy.Services.Wireplumber;

namespace SonicEddy.Services.MixerServiceV2;

public class MixerEditor(IWireplumberService wireplumberService,
    IExternalEffectService externalEffectService)
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

        var oldProcessor = channel.InsertProcessor;
        var newChannel = channel with
        {
            InsertProcessor =
                new FilterChainInsertProcessor(filterChain, filterGraph)
        };

        var newList = mixerLayer.Channels.Select(c =>
            c.ChannelId == channelId ? newChannel : c).ToList();

        oldProcessor?.Destroy();
        return mixerLayer with
        {
            Channels = newList
        };
    }

    public async Task<MixerLayer> AddExternalEffectToChannelStrip(
        MixerLayer mixerLayer, ulong channelId, Guid effectId)
    {
        var channel = mixerLayer.Channels.First(candidate =>
            candidate.ChannelId == channelId);
        var processor = await externalEffectService.CreateInsertAsync(effectId,
            channel.PreFxLooper.PlaybackNode,
            channel.OutputLoopback.CaptureNode,
            $"Layer {mixerLayer.layerId + 1} Channel {channelId}");
        RetargetChannelProcessor(channel.PreFxLooper.PlaybackNode,
            channel.OutputLoopback.CaptureNode, channel.SendLoopbacks,
            processor);
        var oldProcessor = channel.InsertProcessor;
        var replacement = channel with { InsertProcessor = processor };
        oldProcessor?.Destroy();
        return mixerLayer with
        {
            Channels = mixerLayer.Channels.Select(candidate =>
                    candidate.ChannelId == channelId ? replacement : candidate)
                .ToList()
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

        var oldProcessor = channel.InsertProcessor;
        var newChannel = channel with
        {
            InsertProcessor =
                new FilterChainInsertProcessor(filterChain, filterGraph)
        };

        var newList = mixerLayer.GroupChannels.Select(c =>
            c.ChannelId == channelId ? newChannel : c).ToList();

        oldProcessor?.Destroy();
        return mixerLayer with { GroupChannels = newList };
    }

    public async Task<MixerLayer> AddExternalEffectToGroupChannel(
        MixerLayer mixerLayer, ulong channelId, Guid effectId)
    {
        var channel = mixerLayer.GroupChannels.First(candidate =>
            candidate.ChannelId == channelId);
        var processor = await externalEffectService.CreateInsertAsync(effectId,
            channel.PreFxLooper.PlaybackNode,
            channel.OutputLoopback.CaptureNode,
            $"Layer {mixerLayer.layerId + 1} Group {channelId}");
        RetargetChannelProcessor(channel.PreFxLooper.PlaybackNode,
            channel.OutputLoopback.CaptureNode, channel.SendLoopbacks,
            processor);
        var oldProcessor = channel.InsertProcessor;
        var replacement = channel with { InsertProcessor = processor };
        oldProcessor?.Destroy();
        return mixerLayer with
        {
            GroupChannels = mixerLayer.GroupChannels.Select(candidate =>
                    candidate.ChannelId == channelId ? replacement : candidate)
                .ToList()
        };
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

        var oldProcessor = channel.InsertProcessor;
        var newMaster = channel with
        {
            InsertProcessor =
                new FilterChainInsertProcessor(filterChain, filterGraph)
        };

        oldProcessor?.Destroy();
        return mixerLayer with { MasterChannel = newMaster };
    }

    public async Task<MixerLayer> AddExternalEffectToMasterChannel(
        MixerLayer mixerLayer, Guid effectId)
    {
        var channel = mixerLayer.MasterChannel;
        var processor = await externalEffectService.CreateInsertAsync(effectId,
            channel.PreFxLooper.PlaybackNode,
            channel.OutputLoopback.CaptureNode,
            $"Layer {mixerLayer.layerId + 1} Master");
        RetargetChannelProcessor(channel.PreFxLooper.PlaybackNode,
            channel.OutputLoopback.CaptureNode, [], processor);
        var oldProcessor = channel.InsertProcessor;
        var replacement = channel with { InsertProcessor = processor };
        oldProcessor?.Destroy();
        return mixerLayer with { MasterChannel = replacement };
    }

    public MixerLayer RemoveFilterFromChannelStrip(MixerLayer mixerLayer,
        ulong channelId)
    {
        var channel = mixerLayer.Channels.First(c => c.ChannelId == channelId);
        if (channel.InsertProcessor is null) return mixerLayer;

        channel.PreFxLooper.PlaybackNode.OverrideTargetObject(
            channel.OutputLoopback.CaptureNode.ObjectSerial.ToString());
        channel.OutputLoopback.CaptureNode.OverrideTargetObject(
            channel.PreFxLooper.PlaybackNode.ObjectSerial.ToString());
        foreach (var send in channel.SendLoopbacks)
            send.CaptureNode.OverrideTargetObject(
                channel.PreFxLooper.PlaybackNode.ObjectSerial.ToString());

        channel.InsertProcessor.Destroy();
        var replacement = channel with { InsertProcessor = null };
        return mixerLayer with
        {
            Channels = mixerLayer.Channels.Select(candidate =>
                candidate.ChannelId == channelId ? replacement : candidate)
                .ToList()
        };
    }

    public MixerLayer RemoveFilterFromGroupChannel(MixerLayer mixerLayer,
        ulong channelId)
    {
        var channel =
            mixerLayer.GroupChannels.First(c => c.ChannelId == channelId);
        if (channel.InsertProcessor is null) return mixerLayer;

        channel.PreFxLooper.PlaybackNode.OverrideTargetObject(
            channel.OutputLoopback.CaptureNode.ObjectSerial.ToString());
        channel.OutputLoopback.CaptureNode.OverrideTargetObject(
            channel.PreFxLooper.PlaybackNode.ObjectSerial.ToString());

        channel.InsertProcessor.Destroy();
        var replacement = channel with { InsertProcessor = null };
        return mixerLayer with
        {
            GroupChannels = mixerLayer.GroupChannels.Select(candidate =>
                candidate.ChannelId == channelId ? replacement : candidate)
                .ToList()
        };
    }

    public MixerLayer RemoveFilterFromMasterChannel(MixerLayer mixerLayer)
    {
        var channel = mixerLayer.MasterChannel;
        if (channel.InsertProcessor is null) return mixerLayer;

        channel.PreFxLooper.PlaybackNode.OverrideTargetObject(
            channel.OutputLoopback.CaptureNode.ObjectSerial.ToString());
        channel.OutputLoopback.CaptureNode.OverrideTargetObject(
            channel.PreFxLooper.PlaybackNode.ObjectSerial.ToString());
        channel.InsertProcessor.Destroy();
        return mixerLayer with
        {
            MasterChannel =
                channel with { InsertProcessor = null }
        };
    }

    public async Task<MicChannel> AddFilterToMicChannel(MicChannel channel,
        Node globalMasterCaptureNode, FilterGraph filterGraph)
    {
        var filterChainConfig = new FilterChainModuleConfig()
        {
            CaptureProps = new()
            {
                Name = "mixer-fc-mic-capture",
                Description = "Capture Node for Mixer Filter Mic Channel",
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
                Name = "mixer-fc-mic-playback",
                Description = "Playback Node for Mixer Filter Mic Channel",
                Linger = true,
                AutoConnect = false,
                DontFallback = true,
                Passive = false,
                MediaClass = "Stream/Output/Audio",
                AudioPosition = ["FL", "FR"]
            },
            FilterGraph = filterGraph.ToFilterGraphConfig()
        };

        var filterChain =
            await Fr.Sonic.FrSonic.ModuleFactory
                .CreateFilterChainAsync("mixer-fc-mic", filterChainConfig);

        channel.InputLoopback.PlaybackNode.OverrideTargetObject(
            filterChain.CaptureNode.ObjectSerial.ToString());

        UnlinkMicOutputFromGlobalMaster(channel.InputLoopback.PlaybackNode,
            globalMasterCaptureNode);
        await LinkMicOutputToGlobalMaster(filterChain.PlaybackNode,
            globalMasterCaptureNode);

        var oldProcessor = channel.InsertProcessor;
        var newChannel = channel with
        {
            InsertProcessor =
                new FilterChainInsertProcessor(filterChain, filterGraph)
        };

        oldProcessor?.Destroy();
        return newChannel;
    }

    public async Task<MicChannel> AddExternalEffectToMicChannel(
        MicChannel channel, Node globalMasterCaptureNode, Guid effectId)
    {
        var processor = await externalEffectService.CreateInsertAsync(effectId,
            channel.InputLoopback.PlaybackNode,
            globalMasterCaptureNode,
            "Mic");
        channel.InputLoopback.PlaybackNode.OverrideTargetObject(
            processor.InputNode.ObjectSerial.ToString());

        UnlinkMicOutputFromGlobalMaster(channel.InputLoopback.PlaybackNode,
            globalMasterCaptureNode);
        UnlinkMicOutputFromGlobalMaster(processor.OutputNode,
            globalMasterCaptureNode);
        await LinkMicOutputToGlobalMaster(processor.OutputNode,
            globalMasterCaptureNode);

        var oldProcessor = channel.InsertProcessor;
        var replacement = channel with { InsertProcessor = processor };
        oldProcessor?.Destroy();
        return replacement;
    }

    public async Task<MicChannel> RemoveFilterFromMicChannel(
        MicChannel channel, Node globalMasterCaptureNode)
    {
        if (channel.InsertProcessor is null) return channel;

        var oldOutput = channel.InsertProcessor.OutputNode;
        channel.InputLoopback.PlaybackNode.OverrideTargetObject(
            oldOutput.ObjectSerial.ToString());

        UnlinkMicOutputFromGlobalMaster(oldOutput, globalMasterCaptureNode);
        await LinkMicOutputToGlobalMaster(channel.InputLoopback.PlaybackNode,
            globalMasterCaptureNode);

        channel.InsertProcessor.Destroy();
        return channel with { InsertProcessor = null };
    }

    private static async Task LinkMicOutputToGlobalMaster(Node source,
        Node globalMasterCaptureNode)
    {
        var outputPorts = await WaitForPortsAsync(source, "out", 1);
        var inputPorts = Fr.Sonic.FrSonic.PortRegistry.Objects
            .Where(p => p.Node.Id == globalMasterCaptureNode.ObjectId &&
                        p.Direction == "in")
            .OrderBy(p => p.PortId)
            .ToList();

        if (outputPorts.Count == 0) return;

        for (var i = 0; i < inputPorts.Count; i++)
            Fr.Sonic.FrSonic.LinkFactory.CreateLink(
                outputPorts[i % outputPorts.Count], inputPorts[i]);
    }

    private static Task<List<Port>> WaitForPortsAsync(Node node,
        string direction, int expectedCount)
    {
        List<Port> Snapshot() => Fr.Sonic.FrSonic.PortRegistry.Objects
            .Where(p => p.Node.Id == node.ObjectId && p.Direction == direction)
            .OrderBy(p => p.PortId)
            .ToList();

        var existing = Snapshot();
        if (existing.Count >= expectedCount)
            return Task.FromResult(existing);

        var tcs = new TaskCompletionSource<List<Port>>();

        void OnAdded(Port _)
        {
            var current = Snapshot();
            if (current.Count >= expectedCount)
                tcs.TrySetResult(current);
        }

        Fr.Sonic.FrSonic.PortRegistry.Added += OnAdded;

        return WaitWithTimeout();

        async Task<List<Port>> WaitWithTimeout()
        {
            try
            {
                var completed =
                    await Task.WhenAny(tcs.Task, Task.Delay(2000));
                return completed == tcs.Task ? await tcs.Task : Snapshot();
            }
            finally
            {
                Fr.Sonic.FrSonic.PortRegistry.Added -= OnAdded;
            }
        }
    }

    private static void UnlinkMicOutputFromGlobalMaster(Node source,
        Node globalMasterCaptureNode)
    {
        foreach (var link in Fr.Sonic.FrSonic.LinkRegistry.Objects
                     .Where(l => l.OutputNodeId == source.ObjectId &&
                                 l.InputNodeId == globalMasterCaptureNode.ObjectId)
                     .ToList())
            Fr.Sonic.FrSonic.LinkFactory.DeleteLink(link);
    }

    public async Task<MixerLayer> AddFilterToReturnChannel(
        MixerLayer mixerLayer, int index, FilterGraph filterGraph)
    {
        var channel = mixerLayer.SendReturns[index];
        var filterChain = await Fr.Sonic.FrSonic.ModuleFactory
            .CreateFilterChainAsync($"mixer-fc-return-{index}", new()
            {
                CaptureProps = new()
                {
                    Name = $"mixer-fc-return-{index}-capture",
                    Linger = true,
                    AutoConnect = true,
                    DontFallback = true,
                    TargetObject = channel.InputLoopback.PlaybackNode
                        .ObjectSerial.ToString(),
                    MediaClass = "Stream/Input/Audio",
                    AudioPosition = ["FL", "FR"]
                },
                PlaybackProps = new()
                {
                    Name = $"mixer-fc-return-{index}-playback",
                    Linger = true,
                    AutoConnect = true,
                    DontFallback = true,
                    TargetObject = channel.OutputLoopback.CaptureNode
                        .ObjectSerial.ToString(),
                    MediaClass = "Stream/Output/Audio",
                    AudioPosition = ["FL", "FR"]
                },
                FilterGraph = filterGraph.ToFilterGraphConfig()
            });

        channel.OutputLoopback.CaptureNode.OverrideTargetObject(
            filterChain.PlaybackNode.ObjectSerial.ToString());
        var oldProcessor = channel.InsertProcessor;
        var replacement = channel with
        {
            InsertProcessor =
                new FilterChainInsertProcessor(filterChain, filterGraph)
        };
        oldProcessor?.Destroy();
        var returns = mixerLayer.SendReturns.ToList();
        returns[index] = replacement;
        return mixerLayer with { SendReturns = returns };
    }

    public async Task<MixerLayer> AddExternalEffectToReturnChannel(
        MixerLayer mixerLayer, int index, Guid effectId)
    {
        var channel = mixerLayer.SendReturns[index];
        var processor = await externalEffectService.CreateInsertAsync(effectId,
            channel.InputLoopback.PlaybackNode,
            channel.OutputLoopback.CaptureNode,
            $"Layer {mixerLayer.layerId + 1} Return {index + 1}");
        channel.OutputLoopback.CaptureNode.OverrideTargetObject(
            processor.OutputNode.ObjectSerial.ToString());
        var oldProcessor = channel.InsertProcessor;
        var replacement = channel with { InsertProcessor = processor };
        oldProcessor?.Destroy();
        var returns = mixerLayer.SendReturns.ToList();
        returns[index] = replacement;
        return mixerLayer with { SendReturns = returns };
    }

    private static void RetargetChannelProcessor(Node upstream,
        Node downstream, IEnumerable<LoopbackModule> sends,
        InsertProcessor processor)
    {
        upstream.OverrideTargetObject(processor.InputNode.ObjectSerial.ToString());
        downstream.OverrideTargetObject(
            processor.OutputNode.ObjectSerial.ToString());
        foreach (var send in sends)
            send.CaptureNode.OverrideTargetObject(
                processor.OutputNode.ObjectSerial.ToString());
    }

    public MixerLayer RemoveFilterFromReturnChannel(MixerLayer mixerLayer,
        int index)
    {
        var channel = mixerLayer.SendReturns[index];
        if (channel.InsertProcessor is null) return mixerLayer;

        channel.OutputLoopback.CaptureNode.OverrideTargetObject(
            channel.InputLoopback.PlaybackNode.ObjectSerial.ToString());
        channel.InsertProcessor.Destroy();
        var returns = mixerLayer.SendReturns.ToList();
        returns[index] =
            channel with { InsertProcessor = null };
        return mixerLayer with { SendReturns = returns };
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
                            Plugin = "http://gareus.org/oss/lv2/xfade",
                            Control = new Dictionary<string, object> { ["shape"] = 1.0 }
                        }
                    ],
                    Links = []
                }
            });
    }

    public async Task<GlobalMasterChannel> AddFilterToGlobalMaster(
        GlobalMasterChannel globalMaster, FilterGraph filterGraph)
    {
        var filterChain = await Fr.Sonic.FrSonic.ModuleFactory
            .CreateFilterChainAsync("global-master-insert", new()
            {
                CaptureProps = CaptureBasePropsWithTargetObject(true,
                    globalMaster.CrossFader.PlaybackNode.ObjectSerial.ToString(),
                    "global-master-insert-capture", passive: false),
                PlaybackProps = PlaybackBasePropsWithTargetObject(true,
                    globalMaster.OutputTargetObject.ObjectSerial.ToString(),
                    "global-master-insert-playback"),
                FilterGraph = filterGraph.ToFilterGraphConfig()
            });
        globalMaster.CrossFader.PlaybackNode.OverrideTargetObject(
            filterChain.CaptureNode.ObjectSerial.ToString());
        var oldProcessor = globalMaster.InsertProcessor;
        var replacement = globalMaster with
        {
            InsertProcessor =
                new FilterChainInsertProcessor(filterChain, filterGraph)
        };
        oldProcessor?.Destroy();
        return replacement;
    }

    public async Task<GlobalMasterChannel> AddExternalEffectToGlobalMaster(
        GlobalMasterChannel globalMaster, Guid effectId)
    {
        var processor = await externalEffectService.CreateInsertAsync(effectId,
            globalMaster.CrossFader.PlaybackNode,
            globalMaster.OutputTargetObject,
            "Global Master");
        globalMaster.CrossFader.PlaybackNode.OverrideTargetObject(
            processor.InputNode.ObjectSerial.ToString());
        var oldProcessor = globalMaster.InsertProcessor;
        var replacement = globalMaster with { InsertProcessor = processor };
        oldProcessor?.Destroy();
        return replacement;
    }

    public GlobalMasterChannel RemoveGlobalMasterInsert(
        GlobalMasterChannel globalMaster)
    {
        if (globalMaster.InsertProcessor is null) return globalMaster;
        globalMaster.CrossFader.PlaybackNode.OverrideTargetObject(
            globalMaster.OutputTargetObject.ObjectSerial.ToString());
        globalMaster.InsertProcessor.Destroy();
        return globalMaster with { InsertProcessor = null };
    }

    // The cue filter chain's capture targets the GlobalMaster capture node.
    // In PipeWire a Stream/Input/Audio targeting another Stream/Input/Audio
    // connects to its monitor output ports, giving a pre-xfade tap of each layer:
    // AUX0/1 = Layer A, AUX2/3 = Layer B.
    public async Task<FilterChain> CreateCueFilterChain(
        string globalMasterCaptureSerial, string? cueOutputSerial)
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
                    TargetObject = cueOutputSerial ?? "0",
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
                            Plugin = "http://gareus.org/oss/lv2/xfade",
                            Control = new Dictionary<string, object> { ["shape"] = 1.0 }
                        }
                    ],
                    Links = []
                }
            });
    }

    public async Task<MicChannel> CreateMicChannel(
        Node globalMasterCaptureNode)
    {
        var inputLoopback = await wireplumberService.CreateLoopbackModule(
            "mic-input-loopback", new()
            {
                CaptureProps = CaptureBaseProps(true,
                    "mic-input-loopback-capture", "Mic Input Capture"),
                PlaybackProps = PlaybackBaseProps(false,
                    "mic-input-loopback-playback", "Mic Input Playback")
            });

        inputLoopback.CaptureNode.SetVolumes([1.0, 1.0]);
        inputLoopback.PlaybackNode.SetVolumes([1.0, 1.0]);

        await LinkMicOutputToGlobalMaster(inputLoopback.PlaybackNode,
            globalMasterCaptureNode);

        return new(inputLoopback, null);
    }

    public async Task<MixerLayer> Create(string? defaultMasterName,
        ulong layerId,
        int numberOfChannels,
        int numberOfGroupChannels,
        int numberOfReturnChannels,
        string globalMasterCaptureSerial,
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
        OutputChannel defaultOutput, string globalMasterCaptureSerial)
    {
        var preFxLooper = await CreateLooper(
            layerId,
            0,
            "master",
            "pre",
            null,
            null);

        var masterTarget = globalMasterCaptureSerial;
        var playbackAudioPosition = layerId == 0 ? "AUX0,AUX1" : "AUX2,AUX3";

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
        postFxLooper.PlaybackNode.OverrideTargetObject(globalMasterCaptureSerial);

        preFxLooper.CaptureNode.SetVolumes([1.0, 1.0]);

        return new(
            "Master",
            0,
            preFxLooper,
            preFxLooper,
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

        var ducker = await Fr.Sonic.FrSonic.DuckerFactory.CreateDuckerAsync(
            new DuckerConfig($"mixer-group-{channelId}-ducker-{layerId}",
                $"Group {index} Layer {layerId} Ducker",
                PlaybackTargetObject: masterChannel.InputLoopback.CaptureNode.ObjectSerial.ToString()));

        var postFxLooper = await CreateLooper(
            layerId,
            channelId,
            "group",
            "post",
            null,
            ducker.AudioCaptureNode.ObjectSerial.ToString());

        var preFxLooper = await CreateLooper(
            layerId,
            channelId,
            "group",
            "pre",
            null,
            postFxLooper.CaptureNode.ObjectSerial.ToString());

        var sendLoopbacks = (await Task.WhenAll(Enumerable
            .Range(1, numberOfReturnChannels)
            .Select(i =>
                wireplumberService.CreateLoopbackModule(
                    $"send-loopback-group-{index}-send-{i}", new()
                    {
                        CaptureProps = CaptureBasePropsWithTargetObject(true,
                            ducker.AudioPlaybackNode.ObjectSerial
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

        preFxLooper.CaptureNode.SetVolumes([1.0, 1.0]);
        foreach (var send in sendLoopbacks)
            send.PlaybackNode.SetVolumes([0.0, 0.0]);

        return new(
            $"Group {index}",
            (ulong)id,
            preFxLooper,
            preFxLooper,
            null,
            postFxLooper,
            ducker,
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

        preFxLooper.CaptureNode.SetVolumes([1.0, 1.0]);
        foreach (var send in sendLoopbacks)
            send.PlaybackNode.SetVolumes([0.0, 0.0]);

        return new(
            name,
            id,
            preFxLooper,
            preFxLooper,
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
