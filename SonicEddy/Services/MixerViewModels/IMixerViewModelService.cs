using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.ViewModels.MixerViewModelsV2;
using ChannelStrip = SonicEddy.Services.MixerServiceV2.ChannelStrip;

namespace SonicEddy.Services.MixerViewModels;

public interface IMixerViewModelService
{
    Task<MixerViewModel?> ConvertCurrentMixerToViewModel(
        int layerId,
        string? urlSegment,
        IScreen hostScreen);

    InputChannelViewModel ConvertInputChannel(
        InputChannel channel,
        ICommand selectChannelCommand);

    OutputChannelViewModel ConvertOutputChannel(
        OutputChannel channel,
        ICommand selectChannelCommand);

    ChannelStripViewModel ConvertChannelStrip(
        int layerId,
        ChannelStrip channel,
        ICommand selectedChannelCommand,
        ObservableCollection<IRoutingTarget> audioFromRoutingTargets,
        ObservableCollection<IRoutingTarget> audioToRoutingTargets,
        MasterChannelViewModel masterChannel);
}