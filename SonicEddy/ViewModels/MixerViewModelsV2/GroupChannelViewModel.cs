using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Fr.Sonic.PInvoke;
using Fr.Sonic.Modules.Models;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Midi;
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
    FilterGraph? filterGraph,
    ObservableCollection<IRoutingTarget> audioToRoutingTargets,
    IRoutingTarget? selectedAudioToRoutingTarget,
    IAppDataService appDataService,
    IMixerService mixerService,
    GroupChannel groupChannel,
    IMonitoringService monitoringService,
    int layerId,
    IMidiControllerSetupService midiControllerSetupService)
    : ChannelWithSendsViewModelBase(channelId, text, selectChannelCommand,
            inputLoopback, outputLoopback,
            sendLoopbacks, filterChain, filterGraph, audioToRoutingTargets,
            selectedAudioToRoutingTarget, appDataService, mixerService,
            monitoringService, false, layerId, midiControllerSetupService,
            ChannelType.GroupChannel),
        IGroupChannel
{
    public GroupChannel GroupChannel { get; } = groupChannel;
}