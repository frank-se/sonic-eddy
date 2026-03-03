using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Fr.Wireplumber.Modules.Models;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.AppData;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.Monitoring;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class GroupChannelViewModel(
    ulong channelId,
    string text,
    ICommand selectChannelCommand,
    LoopbackModule inputLoopback,
    LoopbackModule outputLoopback,
    List<LoopbackModule> sendLoopbacks,
    FilterChain? filterChain,
    ObservableCollection<IRoutingTarget> audioToRoutingTargets,
    IRoutingTarget? selectedAudioToRoutingTarget,
    IAppDataService appDataService,
    IMixerService mixerService,
    GroupChannel groupChannel,
    IMonitoringService monitoringService)
    : ChannelWithSendsViewModelBase(channelId, text, selectChannelCommand,
            inputLoopback, outputLoopback,
            sendLoopbacks, filterChain, audioToRoutingTargets,
            selectedAudioToRoutingTarget, appDataService, mixerService,
            monitoringService),
        IGroupChannel
{
    public GroupChannel GroupChannel { get; } = groupChannel;
}