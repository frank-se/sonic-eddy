using System;
using System.Collections.Generic;
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

// The T-bar M/E switcher: hosts the two independent compositor panels
// (PanelA/PanelB, each a self-contained StreamingControlViewModel talking
// to its own compositor instance - see CompositorInstanceNames) and the
// single T-bar control that fans out into four effects on every move:
// - gamepad retargeting, so the gamepad always drives whichever side is
//   currently NOT live (IGamepadService.SetPreviewSide)
// - video-blender Props, so the two compositors' outputs visually
//   cross-dissolve (IVideoBlenderService.Client.SetBlendPosition)
// - mic crossfade, only engaged when the two panels' Mic1/Mic2 checkboxes
//   currently select *different* mics (UpdateMicCrossfade)
// - Soomfon deck row repainting (program/preview scene + object selector -
//   see RepaintDeck), and dispatching the deck's own button presses back
//   into scene/object selection (OnDeckKeyDown)
// Normal (non-mic) audio is untouched by this feature - it keeps flowing
// through Master Out exactly as before.
//
// Deck painting/dispatch is deliberately window-scoped, same as gamepad
// target-switching already is: the deck goes dark when this window closes
// (see Dispose) and repaints when it reopens, rather than becoming an
// always-on app-level concept.
public sealed class MixEffectsSwitcherViewModel : ViewModelBase, IDisposable
{
    private readonly IGamepadService _gamepadService;
    private readonly IVideoBlenderService _videoBlenderService;
    private readonly IMixerService _mixerService;
    private readonly ISoomfonDeckService _deckService;

    public MixEffectsSwitcherViewModel(
        StreamingControlViewModel panelA,
        StreamingControlViewModel panelB,
        IGamepadService gamepadService,
        IVideoBlenderService videoBlenderService,
        IMixerService mixerService,
        ISoomfonDeckService deckService)
    {
        PanelA = panelA;
        PanelB = panelB;
        _gamepadService = gamepadService;
        _videoBlenderService = videoBlenderService;
        _mixerService = mixerService;
        _deckService = deckService;

        _gamepadService.TBarAxisChanged += OnTBarAxisChanged;
        _gamepadService.CycleMicRequested += OnCycleMicRequested;

        PanelA.PropertyChanged += OnPanelPropertyChanged;
        PanelB.PropertyChanged += OnPanelPropertyChanged;
        PanelA.Service.SelectionChanged += OnPanelSelectionChanged;
        PanelB.Service.SelectionChanged += OnPanelSelectionChanged;
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

    // Scene-active-state and combined-object-list changes both surface as
    // PanelA/B property reassignments (Scenes/CameraObjects/ImageObjects -
    // see StreamingControlViewModel.ApplyParams/RebuildObjectSlots), so
    // just repaint unconditionally rather than filtering by property name -
    // this fires rarely enough (scene switches, connect/disconnect) that
    // there's no real cost to always repainting all three rows.
    private void OnPanelPropertyChanged(object? sender, PropertyChangedEventArgs e) => RepaintDeck();

    // Fires from whichever thread called IStreamingControlService.SelectObject
    // (window click: UI thread; gamepad: SDL poll thread; deck: its own read
    // thread) - marshal before touching bound state.
    private void OnPanelSelectionChanged() => Dispatcher.UIThread.Post(RepaintDeck);

    // Deck read thread(s) - marshal before touching bound state/panels.
    private void OnDeckKeyStateChanged(int logicalKey, bool isDown) =>
        Dispatcher.UIThread.Post(() => { if (isDown) DispatchDeckKey(logicalKey); });

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
            var previousValue = field;
            this.RaiseAndSetIfChanged(ref field, value);

            var t01 = Math.Clamp((value + 1.0) / 2.0, 0.0, 1.0);
            _gamepadService.SetPreviewSide(previewIsB: value < 0.0);
            _videoBlenderService.Client?.SetBlendPosition((float)t01);
            UpdateMicCrossfade(value);

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

    // Reads all needed state synchronously on the calling (UI) thread -
    // cheap, no I/O - then hands the actual painting (blocking hidraw
    // writes) off to a background thread so it never blocks the UI.
    private void RepaintDeck()
    {
        var (program, preview) = ResolvePanels();
        var frame = BuildFrame(program, preview);
        Task.Run(() => _deckService.PaintFrame(frame));
    }

    private static List<(int Key, byte[]? Image)> BuildFrame(
        StreamingControlViewModel program, StreamingControlViewModel preview)
    {
        var frame = new List<(int Key, byte[]? Image)>(
            SoomfonDeckLayout.ProgramRow.Length + SoomfonDeckLayout.PreviewRow.Length +
            SoomfonDeckLayout.ObjectRow.Length);

        AddSceneRow(frame, SoomfonDeckLayout.ProgramRow, program, SoomfonDeckImages.Red);
        AddSceneRow(frame, SoomfonDeckLayout.PreviewRow, preview, SoomfonDeckImages.Green);
        AddObjectRow(frame, SoomfonDeckLayout.ObjectRow, preview);

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

    private static void AddObjectRow(List<(int Key, byte[]? Image)> frame, int[] rowKeys,
        StreamingControlViewModel preview)
    {
        var objects = preview.CombinedObjects();
        var selection = preview.Service.CurrentSelection;

        for (var i = 0; i < rowKeys.Length; ++i)
        {
            var isSelected = i < objects.Count && selection is { } sel &&
                              sel.SceneIndex == preview.ActiveSceneIndex &&
                              sel.FlatIndex == objects[i].FlatIndex;
            frame.Add((rowKeys[i], isSelected ? SoomfonDeckImages.Yellow : null));
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
        if (objectIndex >= 0)
        {
            var objects = preview.CombinedObjects();
            if (objectIndex < objects.Count)
                objects[objectIndex].SelectCommand.Execute(null);
        }
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
        PanelA.Service.SelectionChanged -= OnPanelSelectionChanged;
        PanelB.Service.SelectionChanged -= OnPanelSelectionChanged;
        _deckService.KeyStateChanged -= OnDeckKeyStateChanged;

        // Leave the physical deck dark rather than showing stale state -
        // this feature is window-scoped, the deck's connection isn't.
        var blankFrame = new List<(int Key, byte[]? Image)>();
        foreach (var key in SoomfonDeckLayout.AllInteractiveKeys)
            blankFrame.Add((key, null));
        Task.Run(() => _deckService.PaintFrame(blankFrame));

        PanelA.Dispose();
        PanelB.Dispose();
    }
}
