using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using Fr.Sonic.PInvoke;
using Microsoft.Extensions.Logging;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Midi;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.TraktorZ1;
using SonicEddy.Services.Monitoring;
using SonicEddy.ViewModels.MixerViewModelsV2;
using ChannelStrip = SonicEddy.Services.MixerServiceV2.ChannelStrip;

namespace SonicEddy.Services.MixerViewModels;

public class MixerViewModelService(
    IAppDataService appDataService,
    IMixerService mixerService,
    IMonitoringService monitoringService,
    ILogger<MixerViewModelService> logger,
    ILoggerFactory loggerFactory,
    IMidiControllerSetupService controllerSetupService,
    IMidiControllerService midiControllerService,
    ITraktorZ1SetupService traktorZ1SetupService)
    : IMixerViewModelService
{
    public async Task<MixerLayerViewModel?> ConvertCurrentMixerToViewModel(
        int layerId)
    {
        logger.LogTrace("ConvertCurrentMixerToViewModel");

        var mixer = await mixerService.GetAndLock();

        if (mixer is null) return null;

        var layer = mixer.Layers[layerId];

        ObservableCollection<IRoutingTarget> audioFromRoutingTargets = [];
        ObservableCollection<IRoutingTarget>
            audioToRoutingTargetsChannelStrips = [];
        ObservableCollection<IRoutingTarget>
            audioToRoutingTargetsGroupChannels = [];
        ObservableCollection<IRoutingTarget>
            audioToRoutingTargetsMasterChannel = [];

        var mixerLayerLogger =
            loggerFactory.CreateLogger<MixerLayerViewModel>();

        var mixerModel =
            new MixerLayerViewModel(
                mixerService,
                this,
                audioFromRoutingTargets,
                audioToRoutingTargetsChannelStrips,
                audioToRoutingTargetsGroupChannels,
                audioToRoutingTargetsMasterChannel,
                midiControllerService, (ulong)layerId,
                mixerLayerLogger
            );

        try
        {
            var selectChannelCommand = mixerModel.SelectChannelCommand;

            var inputChannels = layer.Inputs.Select(i =>
                ConvertInputChannel(i, selectChannelCommand)).ToList();

            audioFromRoutingTargets.AddRange(
                inputChannels.Select(c =>
                    new RoutingTargetViewModel(c.Name, c)));

            mixerModel.InputChannels = new(inputChannels);

            var outputChannels = layer.Outputs.Select(o =>
                ConvertOutputChannel(o, selectChannelCommand)).ToList();

            mixerModel.OutputChannels = new(outputChannels);

            audioToRoutingTargetsMasterChannel.AddRange(
                outputChannels.Select(c =>
                    new RoutingTargetViewModel(c.Name, c)));

            var masterChannel = ConvertMasterChannel(layerId,
                layer.MasterChannel,
                selectChannelCommand);

            mixerModel.MasterChannels = [masterChannel];

            audioToRoutingTargetsChannelStrips.Add(
                new RoutingTargetViewModel("Master", masterChannel));

            audioToRoutingTargetsGroupChannels.Add(
                new RoutingTargetViewModel("Master", masterChannel));

            var returnChannels = layer.SendReturns.Select((channel, index) =>
                ConvertReturnChannel(channel, selectChannelCommand, layerId,
                    index));

            mixerModel.ReturnChannels = new(returnChannels);

            var groupChannels = layer.GroupChannels.Select(g =>
                ConvertGroupChannel(layerId, g, selectChannelCommand,
                    audioToRoutingTargetsGroupChannels,
                    audioToRoutingTargetsGroupChannels.First())).ToArray();

            mixerModel.GroupChannels = new(groupChannels);

            audioToRoutingTargetsChannelStrips.AddRange(
                groupChannels.Select(g =>
                    new RoutingTargetViewModel(g.Text, g)));

            var channels = layer.Channels.Select(c =>
                ConvertChannelStrip(layerId, c, selectChannelCommand,
                    audioFromRoutingTargets,
                    audioToRoutingTargetsChannelStrips,
                    masterChannel));

            mixerModel.ChannelStrips = new(channels);

            mixerModel.SetupEvents();

        }
        catch (Exception e)
        {
            logger.LogError("Exception {}", e.Message);
        }
        finally
        {
            await mixerService.Unlock();
        }

        return mixerModel;
    }

    private void SetupMidiControllerForLayer(MixerLayer layer)
    {
        controllerSetupService.SetMasterChannelNode(layer.layerId,
            layer.MasterChannel.OutputLoopback.PlaybackNode.ObjectId);

        foreach (var (channel, index) in
                 layer.Channels.Select((c, i) => (c, i)))
        {
            var channelId = (ulong)index +
                            layer.layerId *
                            (ulong)mixerService.NumberOfChannelsPerLayer;

            controllerSetupService.SetChannelNode(ChannelType.Channel,
                channelId, channel.OutputLoopback.PlaybackNode.ObjectId);

            foreach (var (send, sendIndex) in channel.SendLoopbacks.Select((s,
                             i) =>
                         (s, i)))
            {
                controllerSetupService.SetChannelSendNode(ChannelType.Channel,
                    channelId, (ulong)sendIndex, send.PlaybackNode.ObjectId);
            }
        }

        foreach (var (groupChannel, index) in
                 layer.GroupChannels.Select((g, i) => (g, i)))
        {
            var channelId = (ulong)index +
                            layer.layerId * (ulong)mixerService
                                .NumberOfGroupChannelsPerLayer;

            controllerSetupService.SetChannelNode(ChannelType.GroupChannel,
                channelId, groupChannel.OutputLoopback.PlaybackNode.ObjectId);

            foreach (var (send, sendIndex) in
                     groupChannel.SendLoopbacks.Select((s,
                             i) =>
                         (s, i)))
            {
                controllerSetupService.SetChannelSendNode(
                    ChannelType.GroupChannel,
                    channelId, (ulong)sendIndex, send.PlaybackNode.ObjectId);
            }
        }
    }


    private void SetupTraktorZ1ForLayer(MixerLayer layer)
    {
        var side = layer.layerId == 0 ? TraktorZ1Side.Left : TraktorZ1Side.Right;
        traktorZ1SetupService.SetMasterFaderNode(side,
            layer.MasterChannel.OutputLoopback.PlaybackNode);
    }

    public void SetupControllers()
    {
        var mixer = mixerService.CurrentMixer;
        if (mixer is null) return;
        foreach (var layer in mixer.Layers)
        {
            SetupMidiControllerForLayer(layer);
            SetupTraktorZ1ForLayer(layer);
        }
        traktorZ1SetupService.SetXfadeNode(
            mixer.GlobalMaster?.CrossFader.CaptureNode);
        traktorZ1SetupService.SetCueMixNode(
            mixer.Cue?.CrossFader.CaptureNode);
    }

    private GroupChannelViewModel ConvertGroupChannel(int layerId,
        GroupChannel channel,
        ICommand selectedChannelCommand,
        ObservableCollection<IRoutingTarget> audioToRoutingTargets,
        IRoutingTarget selectedAudioToRoutingTarget) =>
        new(channel.ChannelId, channel.Name,
            selectedChannelCommand, channel.InputLoopback,
            channel.OutputLoopback, channel.SendLoopbacks,
            channel.FilterChain, channel.FilterGraph,
            audioToRoutingTargets, selectedAudioToRoutingTarget, appDataService,
            mixerService, channel, monitoringService, layerId,
            controllerSetupService);

    private MasterChannelViewModel ConvertMasterChannel(int layerId,
        MasterChannel channel,
        ICommand selectedChannelCommand
    ) => new(channel.ChannelId, channel.Name,
        selectedChannelCommand, channel.InputLoopback,
        channel.OutputLoopback, channel.FilterChain, channel.FilterGraph,
        appDataService, mixerService, channel, monitoringService, layerId,
        controllerSetupService, traktorZ1SetupService);

    public ChannelStripViewModel ConvertChannelStrip(int layerId,
        ChannelStrip channel,
        ICommand selectedChannelCommand,
        ObservableCollection<IRoutingTarget> audioFromRoutingTargets,
        ObservableCollection<IRoutingTarget> audioToRoutingTargets,
        MasterChannelViewModel masterChannel) =>
        new(channel.ChannelId, channel.Name,
            selectedChannelCommand, channel.InputLoopback,
            channel.OutputLoopback,
            channel.SendLoopbacks, channel.FilterChain, channel.FilterGraph,
            audioFromRoutingTargets,
            audioToRoutingTargets,
            audioToRoutingTargets.First(r => r.Channel == masterChannel),
            channel, appDataService, mixerService, monitoringService, layerId,
            controllerSetupService);

    private ReturnChannelViewModel ConvertReturnChannel(
        ReturnChannel channel,
        ICommand selectChannelCommand,
        int layerId,
        int index) =>
        new(channel.Name, selectChannelCommand,
            channel.InputLoopback, channel.OutputLoopback, channel.FilterChain,
            channel.FilterGraph, monitoringService, appDataService,
            mixerService, layerId, index);

    public MicChannelViewModel ConvertMicChannel(MicChannel channel,
        int index, ICommand selectChannelCommand)
    {
        ObservableCollection<IRoutingTarget> audioFromRoutingTargets = [];
        var inputs = mixerService.CurrentMixer?.Layers.FirstOrDefault()
            ?.Inputs ?? [];
        audioFromRoutingTargets.AddRange(inputs.Select(i =>
        {
            var inputChannel = ConvertInputChannel(i, selectChannelCommand);
            return new RoutingTargetViewModel(inputChannel.Name,
                inputChannel) as IRoutingTarget;
        }));

        return new($"Mic {index}", index, selectChannelCommand, channel,
            audioFromRoutingTargets, appDataService, mixerService,
            monitoringService, controllerSetupService);
    }

    public OutputChannelViewModel
        ConvertOutputChannel(OutputChannel channel,
            ICommand selectChannelCommand) =>
        new(channel.Name, selectChannelCommand,
            channel.CaptureNode, monitoringService);

    public InputChannelViewModel ConvertInputChannel(InputChannel channel,
        ICommand selectChannelCommand) =>
        new(channel.Name, selectChannelCommand,
            channel.PlaybackNode, monitoringService);
}
