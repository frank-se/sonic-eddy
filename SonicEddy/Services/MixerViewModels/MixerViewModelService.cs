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
using SonicEddy.ViewModels.MixerViewModelsV2;
using ChannelStrip = SonicEddy.Services.MixerServiceV2.ChannelStrip;

namespace SonicEddy.Services.MixerViewModels;

public class MixerViewModelService(
    IAppDataService appDataService,
    IMixerService mixerService)
    : IMixerViewModelService
{
    public async Task<MixerViewModel?> ConvertCurrentMixerToViewModel(
        string? urlSegment,
        IScreen hostScreen)
    {
        var mixer = await mixerService.GetAndLock();

        if (mixer is null) return null;

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

            var inputChannels = mixer.Inputs.Select(i =>
                ConvertInputChannel(i, selectChannelCommand)).ToList();

            audioFromRoutingTargets.AddRange(
                inputChannels.Select(c =>
                    new RoutingTargetViewModel(c.Name, c)));

            mixerModel.InputChannels = new(inputChannels);

            var outputChannels = mixer.Outputs.Select(o =>
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

                    if (mixer.MasterChannel.OutputTargetObject is null)
                        return false;

                    return output.CaptureNodeObjectSerial == mixer.MasterChannel
                        .OutputTargetObject.ObjectSerial;
                });

            var masterChannel = ConvertMasterChannel(mixer.MasterChannel,
                selectChannelCommand, audioToRoutingTargetsMasterChannel,
                masterSelectedRoutingTarget ??
                audioToRoutingTargetsMasterChannel.First());

            mixerModel.MasterChannels = [masterChannel];

            audioToRoutingTargetsChannelStrips.Add(
                new RoutingTargetViewModel("Master", masterChannel));

            audioToRoutingTargetsGroupChannels.Add(
                new RoutingTargetViewModel("Master", masterChannel));

            var returnChannels = mixer.SendReturns.Select(s =>
                ConvertReturnChannel(s, selectChannelCommand));

            mixerModel.ReturnChannels = new(returnChannels);

            var groupChannels = mixer.GroupChannels.Select(g =>
                ConvertGroupChannel(g, selectChannelCommand,
                    audioToRoutingTargetsGroupChannels,
                    audioToRoutingTargetsGroupChannels.First())).ToArray();

            mixerModel.GroupChannels = new(groupChannels);

            audioToRoutingTargetsChannelStrips.AddRange(
                groupChannels.Select(g =>
                    new RoutingTargetViewModel(g.Text, g)));

            var channels = mixer.Channels.Select(c =>
                ConvertChannelStrip(c, selectChannelCommand,
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

    private GroupChannelViewModel ConvertGroupChannel(GroupChannel channel,
        ICommand selectedChannelCommand,
        ObservableCollection<IRoutingTarget> audioToRoutingTargets,
        IRoutingTarget selectedAudioToRoutingTarget) =>
        new(channel.ChannelId, channel.Name,
            selectedChannelCommand, channel.InputLoopback,
            channel.OutputLoopback, channel.SendLoopbacks, null,
            audioToRoutingTargets, selectedAudioToRoutingTarget, appDataService,
            mixerService, channel);

    private MasterChannelViewModel ConvertMasterChannel(MasterChannel channel,
        ICommand selectedChannelCommand,
        ObservableCollection<IRoutingTarget> audioToRoutingTargets,
        IRoutingTarget selectedAudioToRoutingTarget
    ) => new(channel.ChannelId, channel.Name,
        selectedChannelCommand, channel.InputLoopback,
        channel.OutputLoopback, channel.FilterChain, audioToRoutingTargets,
        selectedAudioToRoutingTarget,
        appDataService, mixerService, channel);

    public ChannelStripViewModel ConvertChannelStrip(ChannelStrip channel,
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
            channel, appDataService, mixerService);

    private static ReturnChannelViewModel ConvertReturnChannel(
        ReturnChannel channel,
        ICommand selectChannelCommand) =>
        new ReturnChannelViewModel(channel.Name, selectChannelCommand,
            channel.InputLoopback, channel.OutputLoopback, channel.FilterChain);

    public OutputChannelViewModel
        ConvertOutputChannel(OutputChannel channel,
            ICommand selectChannelCommand) =>
        new OutputChannelViewModel(channel.Name, selectChannelCommand,
            channel.CaptureNode);

    public InputChannelViewModel ConvertInputChannel(InputChannel channel,
        ICommand selectChannelCommand) =>
        new InputChannelViewModel(channel.Name, selectChannelCommand,
            channel.PlaybackNode);
}