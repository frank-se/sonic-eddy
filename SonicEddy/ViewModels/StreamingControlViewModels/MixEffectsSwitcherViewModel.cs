using System;
using ReactiveUI;
using SonicEddy.Audio;
using SonicEddy.Services.Gamepad;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.VideoBlender;

namespace SonicEddy.ViewModels.StreamingControlViewModels;

// The T-bar M/E switcher: hosts the two independent compositor panels
// (PanelA/PanelB, each a self-contained StreamingControlViewModel talking
// to its own compositor instance - see CompositorInstanceNames) and the
// single T-bar control that fans out into three effects on every move:
// - gamepad retargeting, so the gamepad always drives whichever side is
//   currently NOT live (IGamepadService.SetPreviewSide)
// - video-blender Props, so the two compositors' outputs visually
//   cross-dissolve (IVideoBlenderService.Client.SetBlendPosition)
// - mic crossfade, only engaged when the two panels' Mic1/Mic2 checkboxes
//   currently select *different* mics (UpdateMicCrossfade)
// Normal (non-mic) audio is untouched by this feature - it keeps flowing
// through Master Out exactly as before.
public sealed class MixEffectsSwitcherViewModel : ViewModelBase, IDisposable
{
    private readonly IGamepadService _gamepadService;
    private readonly IVideoBlenderService _videoBlenderService;
    private readonly IMixerService _mixerService;

    public MixEffectsSwitcherViewModel(
        StreamingControlViewModel panelA,
        StreamingControlViewModel panelB,
        IGamepadService gamepadService,
        IVideoBlenderService videoBlenderService,
        IMixerService mixerService)
    {
        PanelA = panelA;
        PanelB = panelB;
        _gamepadService = gamepadService;
        _videoBlenderService = videoBlenderService;
        _mixerService = mixerService;
    }

    public StreamingControlViewModel PanelA { get; }
    public StreamingControlViewModel PanelB { get; }

    // -1..1, matching PanSlider's default Maximum=1.0 (same range the
    // master/cue crossfaders already use). < 0 => A is live, > 0 => B is
    // live; 0 is the exact midpoint.
    public double TBarValue
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);

            var t01 = Math.Clamp((value + 1.0) / 2.0, 0.0, 1.0);
            _gamepadService.SetPreviewSide(previewIsB: value < 0.0);
            _videoBlenderService.Client?.SetBlendPosition((float)t01);
            UpdateMicCrossfade(value);
        }
    }

    public bool PanelAMic1Selected
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            if (value) PanelAMic2Selected = false;
            UpdateMicCrossfade(TBarValue);
        }
    }

    public bool PanelAMic2Selected
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            if (value) PanelAMic1Selected = false;
            UpdateMicCrossfade(TBarValue);
        }
    }

    public bool PanelBMic1Selected
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            if (value) PanelBMic2Selected = false;
            UpdateMicCrossfade(TBarValue);
        }
    }

    public bool PanelBMic2Selected
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            if (value) PanelBMic1Selected = false;
            UpdateMicCrossfade(TBarValue);
        }
    }

    // Only engages when the two panels currently select *different* mics -
    // same mic (or either panel unset) leaves both mics' faders alone, per
    // the confirmed spec. Bypasses PanAndVolumeViewModelV2.Volume entirely
    // (calls Node.SetVolumes directly, same idiom TraktorZ1Service uses) so
    // the T-bar doesn't fight the channel-strip fader's own outbound writes
    // - the operator just shouldn't hand-adjust that fader while engaged.
    private void UpdateMicCrossfade(double tBarValue)
    {
        int? a = PanelAMic1Selected ? 1 : PanelAMic2Selected ? 2 : null;
        int? b = PanelBMic1Selected ? 1 : PanelBMic2Selected ? 2 : null;
        if (a is null || b is null || a == b) return;

        var mixer = _mixerService.CurrentMixer;
        if (mixer is null) return;

        // Pan.GetGainsFromPanAndVolume(pan, 1.0) IS the equal-power law
        // (cos^2+sin^2=1) - reused here as an A/B mic crossfade instead of
        // an L/R pan split.
        var split = Pan.GetGainsFromPanAndVolume(tBarValue, 1.0);
        PushMicGain(mixer, a.Value, split[0]);
        PushMicGain(mixer, b.Value, split[1]);

        // Any mic neither panel currently selects is forced to 0 while
        // engaged - a no-op today (there are only two mic channels, and
        // {a,b} already covers both), kept general/cheap in case a third
        // mic channel is ever added.
        foreach (var idx in new[] { 1, 2 })
            if (idx != a && idx != b)
                PushMicGain(mixer, idx, 0.0);
    }

    private static void PushMicGain(Mixer mixer, int micIndex, double gain)
    {
        var micChannel = micIndex == 1 ? mixer.Mic : mixer.Mic2;
        if (micChannel is null) return;

        micChannel.InputLoopback.PlaybackNode.SetVolumes(
            Pan.GetGainsFromPanAndVolume(0.0, gain).BoostToExternal());
    }

    public void Dispose()
    {
        PanelA.Dispose();
        PanelB.Dispose();
    }
}
