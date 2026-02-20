using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ReactiveUI;
using SonicEddy.Services.AppData;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.ViewModels.MixerViewModelsV2;

namespace SonicEddy.Services.MixerViewModels;

public class MixerViewModelService(
    IAppDataService appDataService,
    IMixerService mixerService)
    : IMixerViewModelService
{
    private readonly IAppDataService _appDataService = appDataService;
    private readonly IMixerService _mixerService = mixerService;

    public MixerViewModel ConvertMixerToViewModel(Mixer mixer,
        string? urlSegment, IScreen hostScreen)
    {
        var mixerModel = new MixerViewModel(urlSegment, hostScreen);

        var selectChannelCommand = mixerModel.SelectChannelCommand;

        var inputChannels = mixer.Inputs.Select(i =>
            ConvertInputChannel(i, selectChannelCommand)).ToList();

        mixerModel.InputChannels = new(inputChannels);

        var outputChannels = mixer.Outputs.Select(o =>
            ConvertOutputChannel(o, selectChannelCommand)).ToList();

        mixerModel.OutputChannels = new(outputChannels);

        var returnChannels = mixer.SendReturns.Select(s =>
            ConvertReturnChannel(s, selectChannelCommand, inputChannels));

        mixerModel.ReturnChannels = new(returnChannels);

        var channels = mixer.Channels.Select(c =>
            ConvertChannelStrip(c, selectChannelCommand, inputChannels,
                outputChannels));

        mixerModel.ChannelStrips = new(channels);

        return mixerModel;
    }

    private ChannelStripViewModel ConvertChannelStrip(ChannelStrip channel,
        ICommand selectedChannelCommand,
        List<InputChannelViewModel> audioFromRoutingTargets,
        List<OutputChannelViewModel> audioToRoutingTargets) =>
        new ChannelStripViewModel(channel.ChannelId, channel.Name,
            selectedChannelCommand, channel.InputLoopback,
            channel.OutputLoopback,
            channel.SendLoopbacks, channel.FilterChain, audioFromRoutingTargets,
            audioToRoutingTargets,
            audioToRoutingTargets.First(c => c.IsMaster),
            channel, _appDataService, _mixerService);

    private ReturnChannelViewModel ConvertReturnChannel(ReturnChannel channel,
        ICommand selectChannelCommand,
        List<InputChannelViewModel> routingTargets) =>
        new ReturnChannelViewModel(channel.Name, selectChannelCommand,
            channel.InputLoopback, channel.OutputLoopback, channel.FilterChain,
            routingTargets);

    private OutputChannelViewModel
        ConvertOutputChannel(OutputChannel channel,
            ICommand selectChannelCommand) =>
        new OutputChannelViewModel(channel.Name, selectChannelCommand,
            channel.CaptureNode, channel.IsMaster);

    private InputChannelViewModel ConvertInputChannel(InputChannel channel,
        ICommand selectChannelCommand) =>
        new InputChannelViewModel(channel.Name, selectChannelCommand,
            channel.PlaybackNode);
}