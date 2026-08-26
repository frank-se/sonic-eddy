using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using SonicEddy.Audio;
using SonicEddy.Services.Gamepad;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.SoomfonDeck;
using SonicEddy.Services.VideoBlender;

namespace SonicEddy.ViewModels.StreamingControlViewModels;

// One of the downstream node's 5 fixed objects (index 0 = the "video in"
// object showing the blender's output, 1-4 = the 4 overlay images - see
// MixEffectsSwitcherViewModel.DownstreamTies) and its key-tie assignment:
// None (opacity is whatever it was last set to - manual show/hide via the
// object's own Visible toggle is the only control), TieA (opacity fades to
// 1 as the T-bar approaches A, 0 approaching B), or TieB (opposite). Only
// one of TieA/TieB can be set at a time.
public sealed class DownstreamTieViewModel : ViewModelBase
{
    private readonly Action<DownstreamTieViewModel> _onChanged;

    public DownstreamTieViewModel(int objectIndex, string label, Action<DownstreamTieViewModel> onChanged)
    {
        ObjectIndex = objectIndex;
        Label = label;
        _onChanged = onChanged;
    }

    public int ObjectIndex { get; }
    public string Label { get; }

    public bool TieA
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            if (value) TieB = false;
            _onChanged(this);
        }
    }

    public bool TieB
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            if (value) TieA = false;
            _onChanged(this);
        }
    }
}

// The T-bar M/E switcher: hosts the two independent compositor panels
// (PanelA/PanelB, each a self-contained StreamingControlViewModel talking
// to its own compositor instance - see CompositorInstanceNames) and a third
// downstream-effects (DSK) panel whose 5 fixed objects sit after the
// video-blender in the actual pipeline. The T-bar fans out into:
// - video-blender Props, so PanelA/B's outputs visually cross-dissolve
//   (IVideoBlenderService.Client.SetBlendPosition)
// - mic crossfade, only engaged when the two panels' Mic1/Mic2 checkboxes
//   currently select *different* mics (UpdateMicCrossfade)
// - downstream key-tie opacity: every DownstreamTies entry that's tied to
//   A or B gets a continuously-updated "opacity" object_params push as the
//   T-bar moves (UpdateDownstreamTies) - a smooth fade, not a hard cut
// - Soomfon deck row repainting (program/preview scene rows 1/2, key-tie
//   toggle row 3 - see RepaintDeck/DispatchDeckKey)
// Normal (non-mic) audio is untouched by this feature - it keeps flowing
// through Master Out exactly as before.
//
// The gamepad no longer targets PanelA/PanelB at all (see GamepadService's
// own class comment) - it's permanently dedicated to the downstream panel's
// objects instead, so compositor scene objects are edited via mouse click
// in the window only. SetPreviewSide/_targetsB-derived state still feeds
// CycleMic (rotates a panel's mic selection) and the deck's row-3 tie
// toggling (ties to whichever side is currently preview).
//
// Deck painting/dispatch is deliberately window-scoped, same as it always
// was: the deck goes dark when this window closes (see Dispose) and
// repaints when it reopens, rather than becoming an always-on app-level
// concept.
public sealed class MixEffectsSwitcherViewModel : ViewModelBase, IDisposable
{
    private readonly IGamepadService _gamepadService;
    private readonly IVideoBlenderService _videoBlenderService;
    private readonly IMixerService _mixerService;
    private readonly ISoomfonDeckService _deckService;

    public MixEffectsSwitcherViewModel(
        StreamingControlViewModel panelA,
        StreamingControlViewModel panelB,
        StreamingControlViewModel downstreamPanel,
        IGamepadService gamepadService,
        IVideoBlenderService videoBlenderService,
        IMixerService mixerService,
        ISoomfonDeckService deckService)
    {
        PanelA = panelA;
        PanelB = panelB;
        DownstreamPanel = downstreamPanel;
        _gamepadService = gamepadService;
        _videoBlenderService = videoBlenderService;
        _mixerService = mixerService;
        _deckService = deckService;

        // Object indices match cameras/examples/downstream/scene.json's
        // fixed layout: 0 = the full-canvas background video, always on,
        // deliberately NOT tie-able (no reason to key the whole background
        // in/out, and there are only 5 physical deck keys for row 3 anyway).
        // 1 = the smaller overlay-video object (e.g. a headshot/PiP feed),
        // 2-5 = the 4 overlay images - these 5 are the tie-able/deck-mapped
        // ones, matching the deck's 5-key row 3 exactly.
        DownstreamTies =
        [
            new DownstreamTieViewModel(1, "Headshot", OnDownstreamTieChanged),
            new DownstreamTieViewModel(2, "Image 1", OnDownstreamTieChanged),
            new DownstreamTieViewModel(3, "Image 2", OnDownstreamTieChanged),
            new DownstreamTieViewModel(4, "Image 3", OnDownstreamTieChanged),
            new DownstreamTieViewModel(5, "Image 4", OnDownstreamTieChanged),
        ];

        _gamepadService.TBarAxisChanged += OnTBarAxisChanged;
        _gamepadService.CycleMicRequested += OnCycleMicRequested;

        PanelA.PropertyChanged += OnPanelPropertyChanged;
        PanelB.PropertyChanged += OnPanelPropertyChanged;
        _deckService.KeyStateChanged += OnDeckKeyStateChanged;

        // Default double is 0.0 - the exact midpoint, which leaves "which
        // side is program" undefined (a 50/50 blend, an arbitrary panel
        // assignment). Start fully on A instead, so the T-bar, the video
        // blend, and the deck all agree on a well-defined initial state.
        // This already triggers RepaintDeck via the setter below.
        TBarValue = -1.0;
    }

    // TBarAxisChanged fires on the SDL poll thread (see GamepadService) -
    // must marshal to the UI thread before touching a bound property, same
    // convention as ObjectControlPanelViewModel.OnObjectStateChanged.
    private void OnTBarAxisChanged(double value) =>
        Dispatcher.UIThread.Post(() => TBarValue = value);

    // Same threading convention as OnTBarAxisChanged - fires on the SDL
    // poll thread.
    private void OnCycleMicRequested(bool targetsB) =>
        Dispatcher.UIThread.Post(() => CycleMic(targetsB));

    // Rotates the gamepad-targeted panel's Mic1/Mic2 selection through
    // None -> Mic1 -> Mic2 -> None. Setters already enforce mutual
    // exclusivity within a panel (selecting one clears the other), so only
    // the None<->Mic2 transition needs an explicit clear here.
    private void CycleMic(bool targetsB)
    {
        if (!targetsB)
        {
            if (PanelAMic2Selected) PanelAMic2Selected = false;
            else if (PanelAMic1Selected) PanelAMic2Selected = true;
            else PanelAMic1Selected = true;
        }
        else
        {
            if (PanelBMic2Selected) PanelBMic2Selected = false;
            else if (PanelBMic1Selected) PanelBMic2Selected = true;
            else PanelBMic1Selected = true;
        }
    }

    // Scene-active-state changes surface as PanelA/B property reassignments
    // (Scenes is reassigned - and so raises PropertyChanged - every time
    // CompositorParams changes; see StreamingControlViewModel.ApplyParams),
    // so just repaint unconditionally rather than filtering by property
    // name - this fires rarely enough (scene switches, connect/disconnect)
    // that there's no real cost to always repainting all three rows.
    // DownstreamPanel deliberately isn't subscribed here - the deck's row 3
    // now reflects key-tie state (owned right here), not anything about
    // DownstreamPanel's own scene/selection state.
    private void OnPanelPropertyChanged(object? sender, PropertyChangedEventArgs e) => RepaintDeck();

    // Deck read thread(s) - marshal before touching bound state/panels.
    private void OnDeckKeyStateChanged(int logicalKey, bool isDown) =>
        Dispatcher.UIThread.Post(() => { if (isDown) DispatchDeckKey(logicalKey); });

    public StreamingControlViewModel PanelA { get; }
    public StreamingControlViewModel PanelB { get; }
    public StreamingControlViewModel DownstreamPanel { get; }
    public ObservableCollection<DownstreamTieViewModel> DownstreamTies { get; }

    // -1..1, matching PanSlider's default Maximum=1.0 (same range the
    // master/cue crossfaders already use). < 0 => A is live, > 0 => B is
    // live; 0 is the exact midpoint.
    public double TBarValue
    {
        get;
        set
        {
            var previousValue = field;
            this.RaiseAndSetIfChanged(ref field, value);

            var t01 = Math.Clamp((value + 1.0) / 2.0, 0.0, 1.0);
            _gamepadService.SetPreviewSide(previewIsB: value < 0.0);
            _videoBlenderService.Client?.SetBlendPosition((float)t01);
            UpdateMicCrossfade(value);
            UpdateDownstreamTies(t01);

            // Row colors only depend on which side is program/preview (the
            // sign), not the continuous value - PanSlider fires Value very
            // frequently while dragging, and repainting (JPEG + blocking
            // hidraw writes) on every one of those ticks is what made the
            // whole window unusably laggy the first time this shipped.
            if ((value < 0.0) != (previousValue < 0.0))
                RepaintDeck();
        }
    }

    // < 0 => A is program/live, B is preview; matches SetPreviewSide's own
    // "previewIsB: value < 0.0" convention above.
    private (StreamingControlViewModel Program, StreamingControlViewModel Preview) ResolvePanels() =>
        TBarValue < 0.0 ? (PanelA, PanelB) : (PanelB, PanelA);

    private bool PreviewIsB => TBarValue < 0.0;

    // Continuous, unlike RepaintDeck - fires every T-bar tick (cheap single
    // Props write per tied object via CompositorClient.SetObjectParams, not
    // image/hidraw I/O, so this doesn't reintroduce the earlier UI-lag bug).
    private void UpdateDownstreamTies(double t01)
    {
        foreach (var tie in DownstreamTies)
            PushTieOpacity(tie, t01);
    }

    // Also called immediately when a tie toggle itself changes (not just on
    // T-bar movement) - pushes opacity for the *current* T-bar position
    // right away instead of waiting for the next move, and repaints the
    // deck's row 3 to match (a tie can change from the window UI, not just
    // the deck's own button, and either way row 3's highlight must follow).
    private void OnDownstreamTieChanged(DownstreamTieViewModel tie)
    {
        PushTieOpacity(tie, Math.Clamp((TBarValue + 1.0) / 2.0, 0.0, 1.0));
        RepaintDeck();
    }

    private void PushTieOpacity(DownstreamTieViewModel tie, double t01)
    {
        if (!tie.TieA && !tie.TieB) return; // untied - opacity left alone, see class comment

        var opacity = tie.TieA ? 1.0 - t01 : t01;
        DownstreamPanel.Client?.SetObjectParams(tie.ObjectIndex, new { opacity });
    }

    // Reads all needed state synchronously on the calling (UI) thread -
    // cheap, no I/O - then hands the actual painting (blocking hidraw
    // writes) off to a background thread so it never blocks the UI.
    private void RepaintDeck()
    {
        var frame = BuildFrame();
        Task.Run(() => _deckService.PaintFrame(frame));
    }

    private List<(int Key, byte[]? Image)> BuildFrame()
    {
        var (program, preview) = ResolvePanels();

        var frame = new List<(int Key, byte[]? Image)>(
            SoomfonDeckLayout.ProgramRow.Length + SoomfonDeckLayout.PreviewRow.Length +
            SoomfonDeckLayout.ObjectRow.Length);

        AddSceneRow(frame, SoomfonDeckLayout.ProgramRow, program, SoomfonDeckImages.Red);
        AddSceneRow(frame, SoomfonDeckLayout.PreviewRow, preview, SoomfonDeckImages.Green);
        AddTieRow(frame, SoomfonDeckLayout.ObjectRow);

        return frame;
    }

    private static void AddSceneRow(List<(int Key, byte[]? Image)> frame, int[] rowKeys,
        StreamingControlViewModel panel, byte[] activeImage)
    {
        var scenes = panel.Scenes;
        for (var i = 0; i < rowKeys.Length; ++i)
        {
            var isActive = i < scenes.Count && !scenes[i].IsEmpty && scenes[i].IsActive;
            frame.Add((rowKeys[i], isActive ? activeImage : null));
        }
    }

    // Row 3: lit exactly when that downstream object is tied to whichever
    // side is currently preview - flips row-to-row as the T-bar crosses,
    // like rows 1/2 already do.
    private void AddTieRow(List<(int Key, byte[]? Image)> frame, int[] rowKeys)
    {
        var previewIsB = PreviewIsB;
        for (var i = 0; i < rowKeys.Length && i < DownstreamTies.Count; ++i)
        {
            var tie = DownstreamTies[i];
            var isTiedToPreview = previewIsB ? tie.TieB : tie.TieA;
            frame.Add((rowKeys[i], isTiedToPreview ? SoomfonDeckImages.Yellow : null));
        }
    }

    private void DispatchDeckKey(int logicalKey)
    {
        var (program, preview) = ResolvePanels();

        var programIndex = Array.IndexOf(SoomfonDeckLayout.ProgramRow, logicalKey);
        if (programIndex >= 0)
        {
            ExecuteSceneSelect(program, programIndex);
            return;
        }

        var previewIndex = Array.IndexOf(SoomfonDeckLayout.PreviewRow, logicalKey);
        if (previewIndex >= 0)
        {
            ExecuteSceneSelect(preview, previewIndex);
            return;
        }

        var objectIndex = Array.IndexOf(SoomfonDeckLayout.ObjectRow, logicalKey);
        if (objectIndex >= 0 && objectIndex < DownstreamTies.Count)
            ToggleDeckTie(DownstreamTies[objectIndex]);
    }

    // Toggle semantics: if not currently tied to whichever side is preview,
    // tie it there (overwriting any existing opposite-side tie); if already
    // tied to the preview side, untie it.
    private void ToggleDeckTie(DownstreamTieViewModel tie)
    {
        if (PreviewIsB)
            tie.TieB = !tie.TieB;
        else
            tie.TieA = !tie.TieA;
    }

    private static void ExecuteSceneSelect(StreamingControlViewModel panel, int index)
    {
        var scenes = panel.Scenes;
        if (index < scenes.Count && !scenes[index].IsEmpty)
            scenes[index].SelectCommand.Execute(null);
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
        _gamepadService.TBarAxisChanged -= OnTBarAxisChanged;
        _gamepadService.CycleMicRequested -= OnCycleMicRequested;
        PanelA.PropertyChanged -= OnPanelPropertyChanged;
        PanelB.PropertyChanged -= OnPanelPropertyChanged;
        _deckService.KeyStateChanged -= OnDeckKeyStateChanged;

        // Leave the physical deck dark rather than showing stale state -
        // this feature is window-scoped, the deck's connection isn't.
        var blankFrame = new List<(int Key, byte[]? Image)>();
        foreach (var key in SoomfonDeckLayout.AllInteractiveKeys)
            blankFrame.Add((key, null));
        Task.Run(() => _deckService.PaintFrame(blankFrame));

        PanelA.Dispose();
        PanelB.Dispose();
        DownstreamPanel.Dispose();
    }
}
