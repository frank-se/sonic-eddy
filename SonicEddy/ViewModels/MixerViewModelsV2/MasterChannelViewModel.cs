using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Fr.Wireplumber.Modules.Models;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.AppData;
using SonicEddy.Services.MixerServiceV2;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class MasterChannelViewModel(
    ulong channelId,
    string text,
    ICommand selectChannelCommand,
    LoopbackModule inputLoopback,
    LoopbackModule outputLoopback,
    FilterChain? filterChain,
    ObservableCollection<IRoutingTarget> audioToRoutingTargets,
    IRoutingTarget? selectedAudioToRoutingTarget,
    IAppDataService appDataService,
    IMixerService mixerService,
    MasterChannel masterChannel)
    : ChannelViewModelBase(channelId, text, selectChannelCommand,
            inputLoopback, outputLoopback, filterChain, audioToRoutingTargets,
            selectedAudioToRoutingTarget, appDataService, mixerService),
        IMasterChannel
{
    public MasterChannel MasterChannel { get; } = masterChannel;
}