using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables.Fluent;
using System.Windows.Input;
using Fr.Pw.Midi.PInvoke;
using Fr.Wireplumber.Model.Props;
using Fr.Wireplumber.Modules.Models;
using ReactiveUI;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Midi;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.Monitoring;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public abstract class ChannelWithSendsViewModelBase : ChannelViewModelBase
{
    protected ChannelWithSendsViewModelBase(ulong channelId, string text,
        ICommand selectChannelCommand, LoopbackModule inputLoopback,
        LoopbackModule outputLoopback, List<LoopbackModule> sendLoopbacks,
        FilterChain? filterChain,
        FilterGraph? filterGraph,
        ObservableCollection<IRoutingTarget> audioToRoutingTargets,
        IRoutingTarget? selectedAudioToRoutingTarget,
        IAppDataService appDataService, IMixerService mixerService,
        IMonitoringService monitoringService,
        bool isMonitoringEnabled, int layerId,
        IMidiControllerSetupService midiControllerSetupService,
        ChannelType channelType) : base(
        channelId, text, selectChannelCommand, inputLoopback, outputLoopback,
        filterChain, filterGraph, audioToRoutingTargets,
        selectedAudioToRoutingTarget, appDataService,
        mixerService, monitoringService, isMonitoringEnabled, layerId,
        midiControllerSetupService, channelType)
    {
        SendLoopbacks = sendLoopbacks;

        this.WhenAnyValue(x => x.Send1Trim)
            .Subscribe(trim => { SetVolumesForSend(0, trim); })
            .DisposeWith(Disposables);

        this.WhenAnyValue(x => x.Send2Trim)
            .Subscribe(trim => { SetVolumesForSend(1, trim); })
            .DisposeWith(Disposables);

        this.WhenAnyValue(x => x.Send3Trim)
            .Subscribe(trim => { SetVolumesForSend(2, trim); })
            .DisposeWith(Disposables);

        this.WhenAnyValue(x => x.Send4Trim)
            .Subscribe(trim => { SetVolumesForSend(3, trim); })
            .DisposeWith(Disposables);

        if (SendLoopbacks.Count > 0)
            SendLoopbacks[0].PlaybackNode.PropertiesChanged +=
                OnSend1PropertiesChanged;

        if (SendLoopbacks.Count > 1)
            SendLoopbacks[1].PlaybackNode.PropertiesChanged +=
                OnSend2PropertiesChanged;

        if (SendLoopbacks.Count > 2)
            SendLoopbacks[2].PlaybackNode.PropertiesChanged +=
                OnSend3PropertiesChanged;

        if (SendLoopbacks.Count > 3)
            SendLoopbacks[3].PlaybackNode.PropertiesChanged +=
                OnSend4PropertiesChanged;
    }

    protected readonly List<LoopbackModule> SendLoopbacks;

    public double Send1Trim
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double Send2Trim
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double Send3Trim
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double Send4Trim
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    private List<bool> _sendsChanging = [false, false, false, false];

    private void OnSend1PropertiesChanged(Properties? properties)
    {
        _sendsChanging[0] = true;
        Send1Trim = CalcSendTrimFromProperties(properties);
        _sendsChanging[0] = false;
    }

    private void OnSend2PropertiesChanged(Properties? properties)
    {
        _sendsChanging[1] = true;
        Send2Trim = CalcSendTrimFromProperties(properties);
        _sendsChanging[1] = false;
    }

    private void OnSend3PropertiesChanged(Properties? properties)
    {
        _sendsChanging[2] = true;
        Send3Trim = CalcSendTrimFromProperties(properties);
        _sendsChanging[2] = false;
    }

    private void OnSend4PropertiesChanged(Properties? properties)
    {
        _sendsChanging[3] = true;
        Send4Trim = CalcSendTrimFromProperties(properties);
        _sendsChanging[3] = false;
    }

    private static float CalcSendTrimFromProperties(Properties? properties)
    {
        if (properties is null) return 0.0f;

        var volumes =
            properties.Channels.Select(c => (double)c.Volume).ToArray();

        if (volumes.Length < 2)
        {
            return 0.0f;
        }
        else
        {
            return (float)volumes.First();
        }
    }

    private void SetVolumesForSend(int index, double volume)
    {
        if (SendLoopbacks.Count > index && !_sendsChanging[index])
            SendLoopbacks[index].PlaybackNode.SetVolumes([volume, volume]);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(true);

        SendLoopbacks[0].PlaybackNode.PropertiesChanged -=
            OnSend1PropertiesChanged;

        SendLoopbacks[1].PlaybackNode.PropertiesChanged -=
            OnSend1PropertiesChanged;

        SendLoopbacks[2].PlaybackNode.PropertiesChanged -=
            OnSend1PropertiesChanged;

        SendLoopbacks[3].PlaybackNode.PropertiesChanged -=
            OnSend1PropertiesChanged;
    }
}