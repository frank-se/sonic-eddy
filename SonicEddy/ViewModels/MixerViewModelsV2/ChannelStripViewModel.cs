using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables.Fluent;
using System.Windows.Input;
using Fr.Sonic.PInvoke;
using Fr.Sonic.Modules.Models;
using ReactiveUI;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Midi;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.Monitoring;
using ChannelStrip = SonicEddy.Services.MixerServiceV2.ChannelStrip;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class ChannelStripViewModel : ChannelWithSendsViewModelBase,
    IChannelStrip
{
    public ChannelStripViewModel(ulong channelId, string text,
        ICommand selectChannelCommand, TwoNodePipewireModule inputLoopback,
        TwoNodePipewireModule outputLoopback, List<LoopbackModule> sendLoopbacks,
        FilterChain? filterChain,
        FilterGraph? filterGraph,
        ObservableCollection<IRoutingTarget> audioFromRoutingTargets,
        ObservableCollection<IRoutingTarget> audioToRoutingTargets,
        IRoutingTarget? selectedAudioToRoutingTarget,
        ChannelStrip channelStrip, IAppDataService appDataService,
        IMixerService mixerService,
        IMonitoringService monitoringService, int layerId,
        IMidiControllerSetupService midiControllerSetupService) : base(
        channelId,
        text,
        selectChannelCommand, inputLoopback, outputLoopback, sendLoopbacks,
        filterChain, filterGraph, audioToRoutingTargets,
        selectedAudioToRoutingTarget,
        appDataService, mixerService, monitoringService, true, layerId,
        midiControllerSetupService, ChannelType.Channel)
    {
        AudioFromRoutingTargets = audioFromRoutingTargets;
        SelectedAudioToRoutingTarget = selectedAudioToRoutingTarget;
        ChannelStrip = channelStrip;
        Looper = new(channelStrip.PreFxLooper, channelStrip.PostFxLooper);

        this.WhenAnyValue(x => x.SelectedAudioFromRoutingTarget)
            .Subscribe(routingTarget =>
            {
                if (routingTarget?.Channel is not InputChannelViewModel channel)
                    return;
                ChannelStrip.InputLoopback.CaptureNode.OverrideTargetObject(
                    channel.PlaybackNodeObjectSerial.ToString());
            })
            .DisposeWith(Disposables);
    }

    public ChannelStrip ChannelStrip { get; }
    public LooperSectionViewModel Looper { get; }

    public ObservableCollection<IRoutingTarget>
        AudioFromRoutingTargets { get; }

    public void DeleteAction()
    {
        throw new NotImplementedException();
    }

    public IRoutingTarget? SelectedAudioFromRoutingTarget
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    protected override void Dispose(bool disposing)
    {
        Looper.Dispose();
        base.Dispose(disposing);
    }
}
