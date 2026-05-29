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

public class MasterChannelViewModel(
    ulong channelId,
    string text,
    ICommand selectChannelCommand,
    TwoNodePipewireModule inputLoopback,
    TwoNodePipewireModule outputLoopback,
    FilterChain? filterChain,
    FilterGraph? filterGraph,
    ObservableCollection<IRoutingTarget> audioToRoutingTargets,
    IRoutingTarget? selectedAudioToRoutingTarget,
    IAppDataService appDataService,
    IMixerService mixerService,
    MasterChannel masterChannel,
    IMonitoringService monitoringService,
    int layerId,
    IMidiControllerSetupService midiControllerSetupService)
    : ChannelViewModelBase(channelId, text, selectChannelCommand,
            inputLoopback, outputLoopback, filterChain, filterGraph,
            audioToRoutingTargets,
            selectedAudioToRoutingTarget, appDataService, mixerService,
            monitoringService, true, layerId, midiControllerSetupService,
            ChannelType.Channel),
        IMasterChannel
{
    public MasterChannel MasterChannel { get; } = masterChannel;
    public LooperSectionViewModel Looper { get; } =
        new(masterChannel.PreFxLooper, masterChannel.PostFxLooper);

    protected override void Dispose(bool disposing)
    {
        Looper.Dispose();
        base.Dispose(disposing);
    }
}
