using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.AppData;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.Monitoring;
using SonicEddy.ViewModels.MixerViewModelsV2;
using ChannelStrip = SonicEddy.Services.MixerServiceV2.ChannelStrip;

namespace SonicEddy.Services.MixerViewModels;

public class MixerViewModelService(
    IAppDataService appDataService,
    IMixerService mixerService,
    IMonitoringService monitoringService)
    : IMixerViewModelService
{
    public async Task<MixerViewModel?> ConvertCurrentMixerToViewModel(
        int layerId,
        string? urlSegment,
        IScreen hostScreen)
    {
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

        var mixerModel =
            new MixerViewModel(
                urlSegment,
                hostScreen,
                mixerService,
                this,
                audioFromRoutingTargets,
                audioToRoutingTargetsChannelStrips,
                audioToRoutingTargetsGroupChannels,
                audioToRoutingTargetsMasterChannel
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

            var masterSelectedRoutingTarget =
                audioToRoutingTargetsMasterChannel.FirstOrDefault(target =>
                {
                    if (target.Channel is not OutputChannelViewModel output)
                        return false;

                    if (layer.MasterChannel.OutputTargetObject is null)
                        return false;

                    return output.CaptureNodeObjectSerial == layer.MasterChannel
                        .OutputTargetObject.ObjectSerial;
                });

            var masterChannel = ConvertMasterChannel(layerId,
                layer.MasterChannel,
                selectChannelCommand, audioToRoutingTargetsMasterChannel,
                masterSelectedRoutingTarget ??
                audioToRoutingTargetsMasterChannel.First());

            mixerModel.MasterChannels = [masterChannel];

            audioToRoutingTargetsChannelStrips.Add(
                new RoutingTargetViewModel("Master", masterChannel));

            audioToRoutingTargetsGroupChannels.Add(
                new RoutingTargetViewModel("Master", masterChannel));

            var returnChannels = layer.SendReturns.Select(s =>
                ConvertReturnChannel(s, selectChannelCommand));

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
        finally
        {
            await mixerService.Unlock();
        }

        return mixerModel;
    }

    private GroupChannelViewModel ConvertGroupChannel(int layerId,
        GroupChannel channel,
        ICommand selectedChannelCommand,
        ObservableCollection<IRoutingTarget> audioToRoutingTargets,
        IRoutingTarget selectedAudioToRoutingTarget) =>
        new(channel.ChannelId, channel.Name,
            selectedChannelCommand, channel.InputLoopback,
            channel.OutputLoopback, channel.SendLoopbacks, null,
            audioToRoutingTargets, selectedAudioToRoutingTarget, appDataService,
            mixerService, channel, monitoringService, layerId);

    private MasterChannelViewModel ConvertMasterChannel(int layerId,
        MasterChannel channel,
        ICommand selectedChannelCommand,
        ObservableCollection<IRoutingTarget> audioToRoutingTargets,
        IRoutingTarget selectedAudioToRoutingTarget
    ) => new(channel.ChannelId, channel.Name,
        selectedChannelCommand, channel.InputLoopback,
        channel.OutputLoopback, channel.FilterChain, audioToRoutingTargets,
        selectedAudioToRoutingTarget,
        appDataService, mixerService, channel, monitoringService, layerId);

    public ChannelStripViewModel ConvertChannelStrip(int layerId,
        ChannelStrip channel,
        ICommand selectedChannelCommand,
        ObservableCollection<IRoutingTarget> audioFromRoutingTargets,
        ObservableCollection<IRoutingTarget> audioToRoutingTargets,
        MasterChannelViewModel masterChannel) =>
        new ChannelStripViewModel(channel.ChannelId, channel.Name,
            selectedChannelCommand, channel.InputLoopback,
            channel.OutputLoopback,
            channel.SendLoopbacks, channel.FilterChain, audioFromRoutingTargets,
            audioToRoutingTargets,
            audioToRoutingTargets.First(r => r.Channel == masterChannel),
            channel, appDataService, mixerService, monitoringService, layerId);

    private ReturnChannelViewModel ConvertReturnChannel(
        ReturnChannel channel,
        ICommand selectChannelCommand) =>
        new ReturnChannelViewModel(channel.Name, selectChannelCommand,
            channel.InputLoopback, channel.OutputLoopback, channel.FilterChain,
            monitoringService);

    public OutputChannelViewModel
        ConvertOutputChannel(OutputChannel channel,
            ICommand selectChannelCommand) =>
        new OutputChannelViewModel(channel.Name, selectChannelCommand,
            channel.CaptureNode, monitoringService);

    public InputChannelViewModel ConvertInputChannel(InputChannel channel,
        ICommand selectChannelCommand) =>
        new InputChannelViewModel(channel.Name, selectChannelCommand,
            channel.PlaybackNode, monitoringService);
}