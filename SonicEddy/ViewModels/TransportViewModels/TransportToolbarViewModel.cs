using System;
using System.Linq;
using System.Reactive;
using Avalonia.Threading;
using Fr.Sonic;
using Fr.Sonic.Sync;
using ReactiveUI;

namespace SonicEddy.ViewModels.TransportViewModels;

public sealed class TransportToolbarViewModel : ViewModelBase, IDisposable
{
    private const ulong BeatsPerBar = 4;
    private const ulong StopLeadTimeBeats = 4;
    private readonly DispatcherTimer _timer;
    private readonly SyncClient? _syncClient;

    public TransportToolbarViewModel()
    {
        var syncMasterNode = FrSonic.NodeRegistry.Objects
            .FirstOrDefault(n => n.Name == "se.sync_master");

        if (syncMasterNode is not null)
        {
            _syncClient = new SyncClient(syncMasterNode);
            _syncClient.SnapshotChanged += _ => Dispatcher.UIThread.Post(UpdateStatus);
        }

        StartCommand = ReactiveCommand.Create(
            StartPlayback,
            this.WhenAnyValue(x => x.TransportState,
                s => s == SyncTransportState.Stopped));

        StopCommand = ReactiveCommand.Create(
            StopPlayback,
            this.WhenAnyValue(x => x.TransportState,
                s => s == SyncTransportState.Playing));

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => UpdateStatus();
        _timer.Start();

        UpdateStatus();
    }

    public SyncTransportState TransportState
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = SyncTransportState.Stopped;

    public bool IsPlaying
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsNotPlaying
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    public string TransportStateText
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "Stopped";

    public string CurrentBarBeat
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "-";

    public ReactiveCommand<Unit, Unit> StartCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }

    private void StartPlayback()
    {
        if (!TryGetNextBarBeat(out var beat)) return;
        _syncClient?.ScheduleTransportState(beat, SyncTransportState.Playing);
    }

    private void StopPlayback()
    {
        if (!TryGetStopBeat(out var beat)) return;
        _syncClient?.ScheduleTransportState(beat, SyncTransportState.Stopped);
    }

    private bool TryGetNextBarBeat(out ulong beat)
    {
        beat = 0;
        var current = _syncClient?.Snapshot?.CurrentBeat(MonotonicClock.NowNsec());
        if (current is null) return false;
        beat = NextBarBeat(current.Value.Beat);
        return true;
    }

    private bool TryGetStopBeat(out ulong beat)
    {
        beat = 0;
        var current = _syncClient?.Snapshot?.CurrentBeat(MonotonicClock.NowNsec());
        if (current is null) return false;

        beat = NextBarBeat(current.Value.Beat);
        var earliest = current.Value.Beat + StopLeadTimeBeats;
        while (beat < earliest)
            beat += BeatsPerBar;

        return true;
    }

    private void UpdateStatus()
    {
        var snapshot = _syncClient?.Snapshot;
        var current = snapshot?.CurrentBeat(MonotonicClock.NowNsec());

        if (snapshot is null || current is null)
        {
            TransportState = SyncTransportState.Stopped;
            IsPlaying = false;
            IsNotPlaying = true;
            TransportStateText = "Stopped";
            CurrentBarBeat = "-";
            return;
        }

        var beat = current.Value.Beat;
        var state = snapshot.TransportStateAt(beat);

        TransportState = state;
        IsPlaying = state == SyncTransportState.Playing;
        IsNotPlaying = state != SyncTransportState.Playing;
        TransportStateText = state switch
        {
            SyncTransportState.Playing => "Playing",
            SyncTransportState.StartScheduled => "Scheduled",
            _ => "Stopped"
        };
        CurrentBarBeat = $"{beat / BeatsPerBar + 1}.{beat % BeatsPerBar + 1}";
    }

    private static ulong NextBarBeat(ulong currentBeat)
    {
        var next = currentBeat + 1;
        while (next % BeatsPerBar != 0) ++next;
        return next;
    }

    public void Dispose()
    {
        _timer.Stop();
        _syncClient?.Dispose();
    }
}
