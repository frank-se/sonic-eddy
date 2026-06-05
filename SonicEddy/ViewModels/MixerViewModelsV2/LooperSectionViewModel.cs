using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Media;
using Fr.Sonic;
using Fr.Sonic.Loopers;
using Fr.Sonic.Modules.Models;
using Fr.Sonic.PInvoke;
using Fr.Sonic.Sync;
using ReactiveUI;
using SonicEddy.Services.Midi;
using SonicEddy.Tools;
using SonicEddy.Views.MixerViewsV2;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public sealed class LooperSectionViewModel : ReactiveObject, IDisposable
{
    private const ulong BeatsPerBar = 4;
    private const uint MaxLoopSlots = 10;

    private readonly Looper _preFxLooper;
    private readonly Looper _postFxLooper;
    private readonly LooperClient _preFxClient;
    private readonly LooperClient _postFxClient;
    private SyncClient? _syncClient;
    private Window? _detailsWindow;
    private readonly IMidiControllerSetupService _midiControllerSetupService;
    private readonly ChannelType _launchpadChannelType;
    private readonly ulong _launchpadChannelId;
    private LooperState? _preFxState;
    private LooperState? _postFxState;
    private readonly Action<LooperState?> _preFxStateChanged;
    private readonly Action<LooperState?> _postFxStateChanged;

    public LooperSectionViewModel(Looper preFxLooper, Looper postFxLooper,
        IMidiControllerSetupService midiControllerSetupService,
        ChannelType launchpadChannelType,
        ulong launchpadChannelId)
    {
        _preFxLooper = preFxLooper;
        _postFxLooper = postFxLooper;
        _midiControllerSetupService = midiControllerSetupService;
        _launchpadChannelType = launchpadChannelType;
        _launchpadChannelId = launchpadChannelId;
        _preFxClient = new LooperClient(preFxLooper.CaptureNode);
        _postFxClient = new LooperClient(postFxLooper.CaptureNode);

        _preFxStateChanged = state => OnLooperStateChanged(false, state);
        _postFxStateChanged = state => OnLooperStateChanged(true, state);
        _preFxClient.StateChanged += _preFxStateChanged;
        _postFxClient.StateChanged += _postFxStateChanged;

        EnsureSyncClient();

        CutPlayCommand = ReactiveCommand.CreateFromTask(CutPlayAsync);
        StopCommand = ReactiveCommand.Create(Stop);
        ShowDetailsCommand = ReactiveCommand.Create(ShowDetails);

        SetMix(_preFxLooper, 0.0);
        SetMix(_postFxLooper, 0.0);
        FrSonicMidi.LaunchpadLoopPressed += OnLaunchpadLoopPressed;
        FrSonicMidi.LaunchpadFaderChanged += OnLaunchpadFaderChanged;
        _ = RefreshLaunchpadStateAsync();
    }

    public ICommand CutPlayCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ShowDetailsCommand { get; }

    public double Mix
    {
        get;
        set
        {
            var mix = Math.Clamp(value, 0.0, 1.0);
            this.RaiseAndSetIfChanged(ref field, mix);
            SetMix(ActiveLooper, mix);
        }
    }

    public int SelectedBarLength
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            RaiseBarLengthProperties();
        }
    } = 1;

    public int[] BarLengths { get; } = [1, 2, 4, 8, 16];

    public bool IsLength1
    {
        get => SelectedBarLength == 1;
        set { if (value) SelectedBarLength = 1; else this.RaisePropertyChanged(nameof(IsLength1)); }
    }

    public bool IsLength2
    {
        get => SelectedBarLength == 2;
        set { if (value) SelectedBarLength = 2; else this.RaisePropertyChanged(nameof(IsLength2)); }
    }

    public bool IsLength4
    {
        get => SelectedBarLength == 4;
        set { if (value) SelectedBarLength = 4; else this.RaisePropertyChanged(nameof(IsLength4)); }
    }

    public bool IsLength8
    {
        get => SelectedBarLength == 8;
        set { if (value) SelectedBarLength = 8; else this.RaisePropertyChanged(nameof(IsLength8)); }
    }

    public bool IsLength16
    {
        get => SelectedBarLength == 16;
        set { if (value) SelectedBarLength = 16; else this.RaisePropertyChanged(nameof(IsLength16)); }
    }

    private void RaiseBarLengthProperties()
    {
        this.RaisePropertyChanged(nameof(IsLength1));
        this.RaisePropertyChanged(nameof(IsLength2));
        this.RaisePropertyChanged(nameof(IsLength4));
        this.RaisePropertyChanged(nameof(IsLength8));
        this.RaisePropertyChanged(nameof(IsLength16));
    }

    public bool IsQuantized
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    public bool IsPostFxSelected
    {
        get;
        set
        {
            if (value == field)
                return;

            if (IsSwitchRejected())
            {
                SwitchRejected = true;
                this.RaisePropertyChanged();
                return;
            }

            SetMix(ActiveLooper, 0.0);
            StopBothLoopers();
            field = value;
            SetMix(ActiveLooper, 0.0);
            Mix = 0.0;
            SwitchRejected = false;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(ActivePositionIcon));
            this.RaisePropertyChanged(nameof(PlayForeground));
            _ = RefreshLaunchpadStateAsync();
        }
    }

    public bool SwitchRejected
    {
        get;
        private set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ActionForeground));
            this.RaisePropertyChanged(nameof(PlayForeground));
        }
    }

    public string ActivePositionIcon => IsPostFxSelected ? "\uF69F" : "\uF695";
    public IBrush ActionForeground => SwitchRejected ? Brushes.Red : Brushes.White;
    public IBrush PlayForeground => SwitchRejected
        ? Brushes.Red
        : ActiveState?.ActivePlayback is not null
            ? Brushes.Green
            : Brushes.White;

    private Looper ActiveLooper => IsPostFxSelected ? _postFxLooper : _preFxLooper;
    private LooperClient ActiveClient => IsPostFxSelected ? _postFxClient : _preFxClient;
    private LooperState? ActiveState => IsPostFxSelected ? _postFxState : _preFxState;

    private async Task CutPlayAsync()
    {
        if (!TryGetTargetBeat(out var beat))
            return;

        var loop = await GetTargetLoopAsync().ConfigureAwait(false);
        var lengthBeats = (ulong)SelectedBarLength * BeatsPerBar;
        var commands = new[]
        {
            new object[] { beat, $"cut {lengthBeats} {loop}" },
            new object[] { beat, $"play {loop}" }
        };

        ActiveLooper.CaptureNode.SetParam("commands", JsonSerializer.Serialize(commands));
    }

    private async Task LaunchpadCutPlayAsync(uint loop)
    {
        if (!TryGetTargetBeat(out var beat))
            return;

        var state = await ActiveClient.GetStateAsync().ConfigureAwait(false);
        var existing = state?.Loops.FirstOrDefault(candidate =>
            candidate.LoopNumber == loop);
        var loaded = IsLoaded(existing);

        object[][] commands = loaded
            ? [[beat, $"play {loop}"]]
            : [[beat, $"cut {(ulong)SelectedBarLength * BeatsPerBar} {loop}"],
               [beat, $"play {loop}"]];

        ActiveLooper.CaptureNode.SetParam("commands",
            JsonSerializer.Serialize(commands));
    }

    private void Stop()
    {
        if (!TryGetTargetBeat(out var beat))
            beat = 0;

        StopLooper(ActiveLooper, beat);
    }

    private void StopBothLoopers()
    {
        if (!TryGetTargetBeat(out var beat))
            beat = 0;

        StopLooper(_preFxLooper, beat);
        StopLooper(_postFxLooper, beat);
    }

    private static void StopLooper(Looper looper, ulong beat)
    {
        var commands = new[] { new object[] { beat, "stop" } };
        looper.CaptureNode.SetParam("commands", JsonSerializer.Serialize(commands));
    }

    private void ShowDetails()
    {
        if (_detailsWindow is not null)
        {
            _detailsWindow.Activate();
            return;
        }

        _detailsWindow = new LooperDetailsWindow
        {
            DataContext = new LooperDetailsViewModel(
                $"{_preFxLooper.Name} / {_postFxLooper.Name}",
                _preFxLooper,
                _postFxLooper)
        };
        _detailsWindow.Closed += OnDetailsWindowClosed;
        var owner = WindowTools.GetMainWindow();
        if (owner is null)
            _detailsWindow.Show();
        else
            _detailsWindow.Show(owner);
    }

    private void OnDetailsWindowClosed(object? sender, EventArgs e)
    {
        if (_detailsWindow is not null)
            _detailsWindow.Closed -= OnDetailsWindowClosed;
        _detailsWindow = null;
    }

    private bool TryGetTargetBeat(out ulong targetBeat)
    {
        EnsureSyncClient();
        var current = _syncClient?.CurrentBeat();
        if (current is null)
        {
            targetBeat = 0;
            return false;
        }

        var next = current.Value.Beat + 1;
        if (!IsQuantized)
        {
            targetBeat = next;
            return true;
        }

        var phraseBeats = (ulong)SelectedBarLength * BeatsPerBar;
        targetBeat = next;
        while (targetBeat % phraseBeats != 0)
            targetBeat++;

        return true;
    }

    private void EnsureSyncClient()
    {
        if (_syncClient is not null)
            return;

        var syncMaster = FrSonic.NodeRegistry.Objects
            .FirstOrDefault(node => node.Name == "se.sync_master");
        if (syncMaster is not null)
            _syncClient = new SyncClient(syncMaster);
    }

    private async Task<uint> GetTargetLoopAsync()
    {
        var state = await ActiveClient.GetStateAsync().ConfigureAwait(false);

        if (state is null)
            return 0;

        for (uint loopNumber = 0; loopNumber < MaxLoopSlots; ++loopNumber)
        {
            var loop = state.Loops.FirstOrDefault(candidate =>
                candidate.LoopNumber == loopNumber);
            if (!IsLoaded(loop))
                return loopNumber;
        }

        var last = MaxLoopSlots - 1;
        ActiveLooper.CaptureNode.SetParam("commands",
            JsonSerializer.Serialize(new[] { new object[] { 0ul, $"archive {last}" } }));
        return last;
    }

    private bool IsSwitchRejected()
    {
        if (Mix == 0.0)
            return false;

        var state = ActiveClient.GetStateAsync().IsCompletedSuccessfully
            ? ActiveClient.GetStateAsync().Result
            : null;

        return state?.ActivePlayback is not null;
    }

    private static void SetMix(Looper looper, double mix) =>
        looper.CaptureNode.SetParam("mix", (float)mix);

    private void OnLooperStateChanged(bool isPostFx, LooperState? state)
    {
        if (isPostFx)
            _postFxState = state;
        else
            _preFxState = state;

        this.RaisePropertyChanged(nameof(PlayForeground));

        if (!SwitchRejected)
        {
            _ = RefreshLaunchpadStateAsync();
            return;
        }

        if (!IsSwitchRejected())
            SwitchRejected = false;

        _ = RefreshLaunchpadStateAsync();
    }

    private async Task RefreshLaunchpadStateAsync()
    {
        var state = await ActiveClient.GetStateAsync().ConfigureAwait(false);
        if (IsPostFxSelected)
            _postFxState = state;
        else
            _preFxState = state;
        this.RaisePropertyChanged(nameof(PlayForeground));

        for (uint loop = 0; loop < 8; loop++)
        {
            var loopState = state?.Loops.FirstOrDefault(candidate =>
                candidate.LoopNumber == loop);
            var loaded = IsLoaded(loopState);
            var playing = state?.ActivePlayback?.LoopNumber == loop;
            _midiControllerSetupService.SetLaunchpadMiniLooperSlotState(
                _launchpadChannelType, _launchpadChannelId, loop, true,
                loaded, playing);
        }
    }

    private static bool IsLoaded(LoopState? loopState) =>
        loopState is not null &&
        !string.Equals(loopState.State, "empty",
            StringComparison.OrdinalIgnoreCase);

    private void OnLaunchpadLoopPressed(LaunchpadLoopPressedEventArgs args)
    {
        if (!IsLaunchpadTarget(args.ChannelType, args.ChannelId))
            return;

        _ = LaunchpadCutPlayAsync((uint)args.LoopPosition);
    }

    private void OnLaunchpadFaderChanged(LaunchpadFaderChangedEventArgs args)
    {
        if (!IsLaunchpadTarget(args.ChannelType, args.ChannelId))
            return;

        Mix = Math.Clamp(args.NormalizedValue, 0.0, 1.0);
    }

    private bool IsLaunchpadTarget(ChannelType channelType, ulong channelId) =>
        channelType == _launchpadChannelType && channelId == _launchpadChannelId;

    public void Dispose()
    {
        if (_detailsWindow is not null)
            _detailsWindow.Close();
        FrSonicMidi.LaunchpadLoopPressed -= OnLaunchpadLoopPressed;
        FrSonicMidi.LaunchpadFaderChanged -= OnLaunchpadFaderChanged;
        _preFxClient.StateChanged -= _preFxStateChanged;
        _postFxClient.StateChanged -= _postFxStateChanged;
        _preFxClient.Dispose();
        _postFxClient.Dispose();
        _syncClient?.Dispose();
    }
}
