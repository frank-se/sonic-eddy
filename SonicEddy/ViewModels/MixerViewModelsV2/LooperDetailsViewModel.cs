using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Fr.Sonic;
using Fr.Sonic.Loopers;
using Fr.Sonic.Modules.Models;
using Fr.Sonic.Sync;
using ReactiveUI;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public sealed class LooperDetailsViewModel : ReactiveObject, IDisposable
{
    private readonly LooperSideDetailsViewModel _preFx;
    private readonly LooperSideDetailsViewModel _postFx;

    public LooperDetailsViewModel(string title, Looper preFxLooper,
        Looper postFxLooper)
    {
        Title = title;
        _preFx = new("Pre FX", preFxLooper);
        _postFx = new("Post FX", postFxLooper);
        Sides = [_preFx, _postFx];
    }

    public string Title { get; }
    public ObservableCollection<LooperSideDetailsViewModel> Sides { get; }

    public void Dispose()
    {
        _preFx.Dispose();
        _postFx.Dispose();
    }
}

public sealed class LooperSideDetailsViewModel : ReactiveObject, IDisposable
{
    private readonly Looper _looper;
    private readonly LooperClient _client;
    private readonly SyncClient? _syncClient;
    private readonly Action<LooperState?> _stateChanged;

    public LooperSideDetailsViewModel(string title, Looper looper)
    {
        Title = title;
        _looper = looper;
        _client = new LooperClient(looper.CaptureNode);
        _stateChanged = state => Dispatcher.UIThread.Post(() => ApplyState(state));
        _client.StateChanged += _stateChanged;

        var syncMaster = FrSonic.NodeRegistry.Objects
            .FirstOrDefault(node => node.Name == "se.sync_master");
        if (syncMaster is not null)
            _syncClient = new SyncClient(syncMaster);

        StopCommand = ReactiveCommand.Create(Stop);

        _ = LoadInitialStateAsync();
    }

    public string Title { get; }
    public ObservableCollection<LooperSlotDetailsViewModel> Slots { get; } = [];
    public ObservableCollection<PendingJobDetailsViewModel> PendingJobs { get; } = [];

    public ICommand StopCommand { get; }

    public string Recording
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "-";

    public string TransportAlignment
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "-";

    public string ActivePlayback
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "-";

    public string LastFailure
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "-";

    private async Task LoadInitialStateAsync()
    {
        var state = await _client.GetStateAsync().ConfigureAwait(false);
        Dispatcher.UIThread.Post(() => ApplyState(state));
    }

    private void ApplyState(LooperState? state)
    {
        Slots.Clear();
        PendingJobs.Clear();

        if (state is null)
        {
            Recording = "-";
            TransportAlignment = "-";
            ActivePlayback = "-";
            LastFailure = "-";
            return;
        }

        Recording = state.Recording ? "recording" : "stopped";
        TransportAlignment = FormatTransportAlignment(state.TransportAlignment);
        ActivePlayback = FormatActivePlayback(state.ActivePlayback);
        LastFailure = FormatLastFailure(state.LastCommandFailure);

        foreach (var loop in state.Loops)
            Slots.Add(new(loop, Play, Archive));

        foreach (var job in state.PendingJobs)
            PendingJobs.Add(new(job));
    }

    private void Stop() => SendCommand("stop");

    private void Play(uint loopNumber)
    {
        SendCommand($"play {loopNumber}");
    }

    private void Archive(uint loopNumber)
    {
        SendCommand($"archive {loopNumber}", immediate: true);
    }

    private void SendCommand(string command, bool immediate = false)
    {
        var beat = immediate ? 0 : TargetBeat();
        var commands = new[] { new object[] { beat, command } };
        _looper.CaptureNode.SetParam("commands",
            JsonSerializer.Serialize(commands));
    }

    private ulong TargetBeat()
    {
        var current = _syncClient?.CurrentBeat();
        return current is null ? 0 : current.Value.Beat + 1;
    }

    private static string FormatTransportAlignment(TransportAlignment alignment)
    {
        var start = alignment.TransportStartBeat?.ToString() ?? "-";
        var zero = alignment.RingBufferZeroBeat?.ToString() ?? "-";
        return $"start {start}, zero {zero}";
    }

    private static string FormatActivePlayback(ActivePlayback? playback)
    {
        if (playback is null)
            return "-";

        var started = playback.StartedAtBeat?.ToString() ?? "-";
        return $"loop {playback.LoopNumber}, gen {playback.Generation}, beat {started}, frame {playback.PlayheadSamples}";
    }

    private static string FormatLastFailure(LastCommandFailure? failure) =>
        failure is null
            ? "-"
            : $"{failure.BeatNumber}: {failure.Command} ({failure.Reason})";

    public void Dispose()
    {
        _client.StateChanged -= _stateChanged;
        _client.Dispose();
        _syncClient?.Dispose();
    }
}

public sealed class LooperSlotDetailsViewModel
{
    public LooperSlotDetailsViewModel(LoopState state, Action<uint> play,
        Action<uint> archive)
    {
        LoopNumber = state.LoopNumber;
        Generation = state.Generation;
        State = state.State;
        Source = state.Source;
        Beats = FormatBeats(state);
        Length = FormatLength(state);
        Format = $"{state.Channels}ch @ {state.SampleRate}Hz";
        Level = $"rms {state.Rms:0.###}, peak {state.Peak:0.###}";
        Range = $"{state.Min:0.###}..{state.Max:0.###}";
        Bpm = state.Bpm?.ToString("0.##") ?? "-";
        CanPlay = string.Equals(state.State, "filled",
            StringComparison.OrdinalIgnoreCase);
        CanArchive = CanPlay;
        PlayCommand = ReactiveCommand.Create(() => play(LoopNumber));
        ArchiveCommand = ReactiveCommand.Create(() => archive(LoopNumber));
    }

    public uint LoopNumber { get; }
    public ulong Generation { get; }
    public string State { get; }
    public string Source { get; }
    public string Beats { get; }
    public string Length { get; }
    public string Format { get; }
    public string Level { get; }
    public string Range { get; }
    public string Bpm { get; }
    public bool CanPlay { get; }
    public bool CanArchive { get; }
    public ICommand PlayCommand { get; }
    public ICommand ArchiveCommand { get; }

    private static string FormatBeats(LoopState state)
    {
        if (state.StartBeat is null || state.EndBeat is null)
            return "-";

        return $"{state.StartBeat}..{state.EndBeat}";
    }

    private static string FormatLength(LoopState state)
    {
        var beats = state.LengthBeats?.ToString() ?? "-";
        return $"{beats} beats, {state.LengthFrames} frames";
    }
}

public sealed class PendingJobDetailsViewModel(PendingJob job)
{
    public string Kind { get; } = job.Kind;
    public string Loop { get; } = job.LoopNumber?.ToString() ?? "-";
    public string Generation { get; } = job.Generation?.ToString() ?? "-";
}
