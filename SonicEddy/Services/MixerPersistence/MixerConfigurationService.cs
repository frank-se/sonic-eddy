using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using SonicEddy.Contracts.Mixers;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.Wireplumber;
using SonicEddy.ViewModels.GlobalMasterViewModels;
using SonicEddy.ViewModels.MixerViewModelsV2;

namespace SonicEddy.Services.MixerPersistence;

public sealed class MixerConfigurationService : IDisposable
{
    private readonly IWireplumberService _wireplumberService;
    private Mixer? _pendingConfiguration;
    private MixerLayerViewModel? _layerA;
    private MixerLayerViewModel? _layerB;
    private GlobalMasterViewModel? _globalMaster;

    public MixerConfigurationService(IWireplumberService wireplumberService)
    {
        _wireplumberService = wireplumberService;
        _wireplumberService.NodeAdded += OnNodeAdded;
    }

    public Mixer Capture(Guid id, string name, MixerLayerViewModel layerA,
        MixerLayerViewModel layerB, GlobalMasterViewModel globalMaster) => new()
    {
        Id = id,
        Name = name,
        Layers =
        [
            CaptureLayer(0, layerA),
            CaptureLayer(1, layerB)
        ],
        MainCrossFader = new()
        {
            Position = globalMaster.Xfade,
            Shape = globalMaster.ShapeIndex,
            Mode = globalMaster.ModeIndex
        },
        CueCrossFader = new()
        {
            Position = globalMaster.CueXfade,
            Shape = globalMaster.CueShapeIndex,
            Mode = globalMaster.CueModeIndex
        },
        Cue = new()
        {
            AudioToNodeName =
                GetRoutingNodeName(
                    globalMaster.CueChannel?.SelectedAudioToRoutingTarget),
            Pan = globalMaster.CueChannel?.PanAndVolume.Pan ?? 0.0,
            Volume = globalMaster.CueChannel?.PanAndVolume.Volume ?? 1.0
        },
        GlobalMasterInsert = globalMaster.CaptureInsertProcessor()
    };

    public Mixer CreateDefault(MixerLayerViewModel layerA,
        MixerLayerViewModel layerB, GlobalMasterViewModel globalMaster)
    {
        var configuration = Capture(Guid.Empty, "Default Mixer", layerA,
            layerB, globalMaster);

        configuration.MainCrossFader = new();
        configuration.CueCrossFader = new();
        configuration.Cue.Pan = 0.0;
        configuration.Cue.Volume = 1.0;

        foreach (var layer in configuration.Layers)
        {
            foreach (var channel in layer.Channels)
                ResetChannel(channel);
            foreach (var group in layer.Groups)
                ResetChannel(group);

            ResetChannel(layer.Master);

            foreach (var channel in layer.Returns)
            {
                channel.Pan = 0.0;
                channel.Volume = 1.0;
            }
        }

        return configuration;
    }

    private static void ResetChannel(ChannelConfig channel)
    {
        channel.Pan = 0.0;
        channel.Volume = 1.0;
        channel.Trim = 0.0;
        channel.Sends = channel.Sends.Select(_ => 0.0).ToList();
        channel.Looper = new();
    }

    public async Task ApplyAsync(Mixer configuration,
        MixerLayerViewModel layerA, MixerLayerViewModel layerB,
        GlobalMasterViewModel globalMaster)
    {
        _pendingConfiguration = configuration;
        _layerA = layerA;
        _layerB = layerB;
        _globalMaster = globalMaster;

        foreach (var layerConfig in configuration.Layers)
        {
            var layer = layerConfig.LayerId == 0 ? layerA : layerB;
            await ApplyLayerAsync(layerConfig, layer);
        }

        globalMaster.Xfade = configuration.MainCrossFader.Position;
        globalMaster.ShapeIndex = configuration.MainCrossFader.Shape;
        globalMaster.ModeIndex = configuration.MainCrossFader.Mode;
        globalMaster.CueXfade = configuration.CueCrossFader.Position;
        globalMaster.CueShapeIndex = configuration.CueCrossFader.Shape;
        globalMaster.CueModeIndex = configuration.CueCrossFader.Mode;
        await globalMaster.ApplyInsertProcessorAsync(
            configuration.GlobalMasterInsert);

        if (globalMaster.CueChannel is not null)
        {
            globalMaster.CueChannel.PanAndVolume.Pan = configuration.Cue.Pan;
            globalMaster.CueChannel.PanAndVolume.Volume =
                configuration.Cue.Volume;
        }

        ApplyPendingRoutes();
    }

    private static MixerLayerConfig CaptureLayer(ulong layerId,
        MixerLayerViewModel layer) => new()
    {
        LayerId = layerId,
        Channels = layer.ChannelStrips?.OfType<ChannelStripViewModel>()
            .Select(CaptureNormalChannel).ToList() ?? [],
        Groups = layer.GroupChannels?.OfType<GroupChannelViewModel>()
            .Select(CaptureChannel).ToList() ?? [],
        Master = layer.MasterChannels?.OfType<MasterChannelViewModel>()
            .Select(CaptureChannel).FirstOrDefault() ?? new(),
        Returns = layer.ReturnChannels?.OfType<ReturnChannelViewModel>()
            .Select((channel, index) => new ReturnChannelConfig
            {
                Index = index,
                InsertProcessor = channel.CaptureFilterConfiguration(),
                Pan = channel.PanAndVolume.Pan,
                Volume = channel.PanAndVolume.Volume
            }).ToList() ?? []
    };

    private static ChannelConfig CaptureNormalChannel(
        ChannelStripViewModel channel)
    {
        var config = CaptureChannel(channel);
        config.AudioFromNodeName =
            GetRoutingNodeName(channel.SelectedAudioFromRoutingTarget);
        config.Trim = channel.Trim;
        return config;
    }

    private static ChannelConfig CaptureChannel(ChannelViewModelBase channel)
    {
        var config = new ChannelConfig
        {
            ChannelId = channel.ChannelId,
            AudioToNodeName =
                GetRoutingNodeName(channel.SelectedAudioToRoutingTarget),
            InsertProcessor = channel.CaptureFilterConfiguration(),
            Pan = channel.PanAndVolume.Pan,
            Volume = channel.PanAndVolume.Volume
        };

        if (channel is ChannelWithSendsViewModelBase withSends)
        {
            config.Sends =
            [
                withSends.Send1Trim,
                withSends.Send2Trim,
                withSends.Send3Trim,
                withSends.Send4Trim
            ];
        }

        var looper = channel switch
        {
            ChannelStripViewModel normal => normal.Looper,
            GroupChannelViewModel group => group.Looper,
            MasterChannelViewModel master => master.Looper,
            _ => null
        };
        if (looper is not null)
        {
            config.Looper = new()
            {
                IsPostFxSelected = looper.IsPostFxSelected,
                Mix = looper.Mix,
                BarLength = looper.SelectedBarLength,
                IsQuantized = looper.IsQuantized
            };
        }

        return config;
    }

    private static async Task ApplyLayerAsync(MixerLayerConfig config,
        MixerLayerViewModel layer)
    {
        foreach (var channelConfig in config.Channels)
        {
            var channel = layer.ChannelStrips?
                .OfType<ChannelStripViewModel>()
                .FirstOrDefault(candidate =>
                    candidate.ChannelId == channelConfig.ChannelId);
            if (channel is null) continue;
            await ApplyChannelAsync(channelConfig, channel);
            channel.Trim = channelConfig.Trim ?? 0.0;
        }

        foreach (var channelConfig in config.Groups)
        {
            var channel = layer.GroupChannels?
                .OfType<GroupChannelViewModel>()
                .FirstOrDefault(candidate =>
                    candidate.ChannelId == channelConfig.ChannelId);
            if (channel is not null)
                await ApplyChannelAsync(channelConfig, channel);
        }

        if (layer.MasterChannels?.OfType<MasterChannelViewModel>()
                .FirstOrDefault() is { } master)
            await ApplyChannelAsync(config.Master, master);

        foreach (var returnConfig in config.Returns)
        {
            if (layer.ReturnChannels?.OfType<ReturnChannelViewModel>()
                    .ElementAtOrDefault(returnConfig.Index) is not { } channel)
                continue;

            await channel.ApplyFilterConfigurationAsync((int)config.LayerId,
                returnConfig.Index, returnConfig.InsertProcessor);
            channel.PanAndVolume.Pan = returnConfig.Pan;
            channel.PanAndVolume.Volume = returnConfig.Volume;
        }
    }

    private static async Task ApplyChannelAsync(ChannelConfig config,
        ChannelViewModelBase channel)
    {
        await channel.ApplyFilterConfigurationAsync(config.InsertProcessor);
        channel.PanAndVolume.Pan = config.Pan;
        channel.PanAndVolume.Volume = config.Volume;

        if (channel is ChannelWithSendsViewModelBase withSends)
        {
            if (config.Sends.Count > 0) withSends.Send1Trim = config.Sends[0];
            if (config.Sends.Count > 1) withSends.Send2Trim = config.Sends[1];
            if (config.Sends.Count > 2) withSends.Send3Trim = config.Sends[2];
            if (config.Sends.Count > 3) withSends.Send4Trim = config.Sends[3];
        }

        var looper = channel switch
        {
            ChannelStripViewModel normal => normal.Looper,
            GroupChannelViewModel group => group.Looper,
            MasterChannelViewModel master => master.Looper,
            _ => null
        };
        if (looper is null) return;

        looper.Mix = 0.0;
        looper.IsPostFxSelected = config.Looper.IsPostFxSelected;
        looper.SelectedBarLength = config.Looper.BarLength;
        looper.IsQuantized = config.Looper.IsQuantized;
        looper.Mix = config.Looper.Mix;
    }

    private void ApplyPendingRoutes()
    {
        if (_pendingConfiguration is null || _layerA is null ||
            _layerB is null || _globalMaster is null)
            return;

        foreach (var layerConfig in _pendingConfiguration.Layers)
        {
            var layer = layerConfig.LayerId == 0 ? _layerA : _layerB;
            foreach (var config in layerConfig.Channels)
            {
                var channel = layer.ChannelStrips?
                    .OfType<ChannelStripViewModel>()
                    .FirstOrDefault(candidate =>
                        candidate.ChannelId == config.ChannelId);
                if (channel is null) continue;
                ApplyAudioFrom(config.AudioFromNodeName, channel, layer);
                ApplyAudioTo(config.AudioToNodeName, channel, layer);
            }

            foreach (var config in layerConfig.Groups)
            {
                var channel = layer.GroupChannels?
                    .OfType<GroupChannelViewModel>()
                    .FirstOrDefault(candidate =>
                        candidate.ChannelId == config.ChannelId);
                if (channel is not null)
                    ApplyAudioTo(config.AudioToNodeName, channel, layer);
            }

            if (layer.MasterChannels?.OfType<MasterChannelViewModel>()
                    .FirstOrDefault() is { } master)
                ApplyAudioTo(layerConfig.Master.AudioToNodeName, master, layer);
        }

        var cue = _globalMaster.CueChannel;
        if (cue is not null)
        {
            var target = cue.AudioToRoutingTargets.FirstOrDefault(candidate =>
                GetRoutingNodeName(candidate) ==
                _pendingConfiguration.Cue.AudioToNodeName);
            if (target is not null)
                cue.SelectedAudioToRoutingTarget = target;
        }
    }

    private static void ApplyAudioFrom(string? nodeName,
        ChannelStripViewModel channel, MixerLayerViewModel layer)
    {
        if (nodeName is null) return;
        var target = layer.AudioFromRoutingTargets.FirstOrDefault(candidate =>
            GetRoutingNodeName(candidate) == nodeName);
        if (target is not null)
            channel.SelectedAudioFromRoutingTarget = target;
    }

    private static void ApplyAudioTo(string? nodeName,
        ChannelViewModelBase channel, MixerLayerViewModel layer)
    {
        if (nodeName is null) return;
        IEnumerable<IRoutingTarget> targets = channel switch
        {
            MasterChannelViewModel => layer.AudioToRoutingTargetsMasterChannel,
            GroupChannelViewModel => layer.AudioToRoutingTargetsGroupChannels,
            _ => layer.AudioToRoutingTargetsChannelStrips
        };
        var target = targets.FirstOrDefault(candidate =>
            GetRoutingNodeName(candidate) == nodeName);
        if (target is not null)
            channel.SelectedAudioToRoutingTarget = target;
    }

    private static string? GetRoutingNodeName(IRoutingTarget? target) =>
        target?.Channel switch
        {
            InputChannelViewModel input => input.PlaybackNodeName,
            OutputChannelViewModel output => output.CaptureNodeName,
            ChannelViewModelBase channel => channel.InputNodeName,
            _ => null
        };

    private void OnNodeAdded(Fr.Sonic.Model.Objects.Node node) =>
        Dispatcher.UIThread.Post(ApplyPendingRoutes);

    public void Dispose()
    {
        _wireplumberService.NodeAdded -= OnNodeAdded;
        GC.SuppressFinalize(this);
    }
}
