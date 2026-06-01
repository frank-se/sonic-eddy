using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.ViewModels.MixerViewModelsV2;

namespace SonicEddy.ViewModels.GlobalMasterViewModels;

public class CueChannelViewModel : ReactiveObject, IDisposable
{
    private readonly Fr.Sonic.Model.Objects.Node _playbackNode;
    private readonly CompositeDisposable _disposables = new();

    public CueChannelViewModel(CueChannel cue,
        ObservableCollection<IRoutingTarget> audioToRoutingTargets)
    {
        _playbackNode = cue.CrossFader.PlaybackNode;
        PanAndVolume = new PanAndVolumeViewModel(_playbackNode);
        AudioToRoutingTargets = audioToRoutingTargets;

        this.WhenAnyValue(x => x.SelectedAudioToRoutingTarget)
            .Subscribe(ApplyAudioRoutingTarget)
            .DisposeWith(_disposables);
    }

    public PanAndVolumeViewModel PanAndVolume { get; }
    public ObservableCollection<IRoutingTarget> AudioToRoutingTargets { get; }

    public IRoutingTarget? SelectedAudioToRoutingTarget
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    private void ApplyAudioRoutingTarget(IRoutingTarget? routingTarget)
    {
        if (routingTarget is null) return;
        if (routingTarget.Channel is OutputChannelViewModel output)
            _playbackNode.OverrideTargetObject(
                output.CaptureNodeObjectSerial.ToString());
    }

    public void Dispose()
    {
        _disposables.Dispose();
        PanAndVolume.Dispose();
        GC.SuppressFinalize(this);
    }
}
