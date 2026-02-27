using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables.Fluent;
using System.Windows.Input;
using Fr.Wireplumber.Model.Props;
using Fr.Wireplumber.Modules.Models;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.AppData;
using SonicEddy.Services.MixerServiceV2;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public abstract class ChannelWithSendsViewModelBase : ChannelViewModelBase
{
    protected ChannelWithSendsViewModelBase(ulong channelId, string text,
        ICommand selectChannelCommand, LoopbackModule inputLoopback,
        LoopbackModule outputLoopback, List<LoopbackModule> sendLoopbacks,
        FilterChain? filterChain,
        ObservableCollection<IRoutingTarget> audioToRoutingTargets,
        IRoutingTarget? selectedAudioToRoutingTarget,
        IAppDataService appDataService, IMixerService mixerService) : base(
        channelId, text, selectChannelCommand, inputLoopback, outputLoopback,
        filterChain, audioToRoutingTargets,
        selectedAudioToRoutingTarget, appDataService,
        mixerService)
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

    private void OnSend1PropertiesChanged(Properties? properties)
    {
        Send1Trim = CalcSendTrimFromProperties(properties);
    }

    private void OnSend2PropertiesChanged(Properties? properties)
    {
        Send2Trim = CalcSendTrimFromProperties(properties);
    }

    private void OnSend3PropertiesChanged(Properties? properties)
    {
        Send3Trim = CalcSendTrimFromProperties(properties);
    }

    private void OnSend4PropertiesChanged(Properties? properties)
    {
        Send4Trim = CalcSendTrimFromProperties(properties);
    }

    private static float CalcSendTrimFromProperties(Properties? properties)
    {
        if (properties is null) return 0.0f;

        var volumes =
            Audio.Pan.AttenuateFromExternal(
                properties.Channels.Select(c => (double)c.Volume)
                    .ToArray());

        if (volumes.Length < 2)
        {
            return 0.0f;
        }
        else
        {
            var (_, volume) =
                Audio.Pan.GetPanAndVolumeFromGains(volumes[0], volumes[1]);

            return (float)volume;
        }
    }

    private void SetVolumesForSend(int index, double volume)
    {
        if (SendLoopbacks.Count > index)
            SendLoopbacks[index].PlaybackNode.SetVolumes(
                Audio.Pan.BoostToExternal(
                    Audio.Pan.GetGainsFromPanAndVolume(0.0, volume)));
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