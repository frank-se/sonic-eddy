using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables.Fluent;
using System.Windows.Input;
using Fr.Wireplumber.Modules.Models;
using ReactiveUI;
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
        ICommand selectChannelCommand, LoopbackModule inputLoopback,
        LoopbackModule outputLoopback, List<LoopbackModule> sendLoopbacks,
        FilterChain? filterChain,
        ObservableCollection<IRoutingTarget> audioFromRoutingTargets,
        ObservableCollection<IRoutingTarget> audioToRoutingTargets,
        IRoutingTarget? selectedAudioToRoutingTarget,
        ChannelStrip channelStrip, IAppDataService appDataService,
        IMixerService mixerService,
        IMonitoringService monitoringService, int layerId) : base(channelId,
        text,
        selectChannelCommand, inputLoopback, outputLoopback, sendLoopbacks,
        filterChain, audioToRoutingTargets, selectedAudioToRoutingTarget,
        appDataService, mixerService, monitoringService, false, layerId)
    {
        AudioFromRoutingTargets = audioFromRoutingTargets;
        SelectedAudioToRoutingTarget = selectedAudioToRoutingTarget;
        ChannelStrip = channelStrip;

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
}