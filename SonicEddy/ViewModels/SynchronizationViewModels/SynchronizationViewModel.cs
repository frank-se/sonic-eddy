using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Sync;
using ReactiveUI;
using SonicEddy.ViewModels;

namespace SonicEddy.ViewModels.SynchronizationViewModels;

public sealed class SynchronizationViewModel : ViewModelBase, IDisposable
{
    private const ulong BeatsPerBar = 4;
    private const ulong StopLeadTimeBeats = 4;
    private readonly DispatcherTimer _timer;
    private readonly SyncClient? _syncClient;

    public SynchronizationViewModel(Node? syncMasterNode)
    {
        if (syncMasterNode is not null)
        {
            _syncClient = new SyncClient(syncMasterNode);
            _syncClient.SnapshotChanged += OnSnapshotChanged;
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        UpdateStatus();
    }

    public ObservableCollection<SynchronizationApplyMode> ApplyModes { get; } =
    [
        SynchronizationApplyMode.NextBar,
        SynchronizationApplyMode.NextBeat,
        SynchronizationApplyMode.InTwoBeats,
        SynchronizationApplyMode.InFourBeats,
        SynchronizationApplyMode.CustomBeat
    ];

    public decimal Bpm
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 120.0m;

    public decimal CustomBeat
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public SynchronizationApplyMode ApplyMode
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsCustomBeatSelected));
            UpdateTargetBeat();
        }
    } = SynchronizationApplyMode.NextBar;

    public bool IsCustomBeatSelected =>
        ApplyMode == SynchronizationApplyMode.CustomBeat;

    public string TransportState
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "unknown";

    public string CurrentBeat
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "-";

    public string CurrentBarBeat
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "-";

    public string ActiveBpm
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "-";

    public string TargetBeat
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "-";

    public string PendingChanges
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "none";

    public void StartPlayback() =>
        ScheduleTransportState(SyncTransportState.Playing);

    public void StopPlayback()
    {
        if (TryGetStopTargetBeat(out var target))
            _syncClient?.ScheduleTransportState(target, SyncTransportState.Stopped);
    }

    public void ApplyBpm()
    {
        if (TryGetTargetBeat(out var target))
            _syncClient?.ScheduleBpm(target, decimal.ToDouble(Bpm));
    }

    private void ScheduleTransportState(SyncTransportState state)
    {
        if (TryGetTargetBeat(out var target))
            _syncClient?.ScheduleTransportState(target, state);
    }

    private void OnSnapshotChanged(SyncSnapshot _) =>
        Dispatcher.UIThread.Post(UpdateStatus);

    private void OnTimerTick(object? sender, EventArgs e) => UpdateStatus();

    private void UpdateStatus()
    {
        var snapshot = _syncClient?.Snapshot;
        if (snapshot is null)
        {
            TransportState = "sync master unavailable";
            CurrentBeat = "-";
            CurrentBarBeat = "-";
            ActiveBpm = "-";
            PendingChanges = "none";
            TargetBeat = "-";
            return;
        }

        var now = MonotonicClock.NowNsec();
        var current = snapshot.CurrentBeat(now);
        if (current is null)
        {
            TransportState = "waiting";
            CurrentBeat = "-";
            CurrentBarBeat = "-";
            ActiveBpm = snapshot.Bpm.Count > 0
                ? $"{snapshot.Bpm[0].Bpm:0.##}"
                : "-";
            UpdateTargetBeat();
            return;
        }

        var beat = current.Value.Beat;
        var state = snapshot.TransportStateAt(beat);
        TransportState = state.ToString();
        CurrentBeat = beat.ToString();
        CurrentBarBeat = $"{beat / BeatsPerBar + 1}.{beat % BeatsPerBar + 1}";
        ActiveBpm = $"{snapshot.BpmAt(beat):0.##}";
        PendingChanges = FormatPendingChanges(snapshot, beat);
        UpdateTargetBeat();
    }

    private void UpdateTargetBeat()
    {
        TargetBeat = TryGetTargetBeat(out var target) ? target.ToString() : "-";
    }

    private bool TryGetTargetBeat(out ulong target)
    {
        target = 0;
        var snapshot = _syncClient?.Snapshot;
        if (snapshot is null)
            return false;

        var current = snapshot.CurrentBeat(MonotonicClock.NowNsec());
        if (current is null)
            return false;

        var beat = current.Value.Beat;
        target = ApplyMode switch
        {
            SynchronizationApplyMode.NextBeat => beat + 1,
            SynchronizationApplyMode.InTwoBeats => beat + 2,
            SynchronizationApplyMode.InFourBeats => beat + 4,
            SynchronizationApplyMode.CustomBeat => decimal.ToUInt64(CustomBeat),
            _ => NextBarBeat(beat)
        };
        return true;
    }

    private bool TryGetStopTargetBeat(out ulong target)
    {
        if (!TryGetTargetBeat(out target))
            return false;

        var current = _syncClient?.Snapshot?.CurrentBeat(MonotonicClock.NowNsec());
        if (current is null)
            return false;

        target = Math.Max(target, current.Value.Beat + StopLeadTimeBeats);
        return true;
    }

    private static ulong NextBarBeat(ulong currentBeat)
    {
        var next = currentBeat + 1;
        while (next % BeatsPerBar != 0)
            ++next;
        return next;
    }

    private static string FormatPendingChanges(SyncSnapshot snapshot, ulong beat)
    {
        var pendingBpm = snapshot.Bpm.Where(entry => entry.Beat > beat)
            .Select(entry => $"BPM {entry.Bpm:0.##} at {entry.Beat}");
        var pendingTransport = snapshot.TransportStates
            .Where(entry => entry.Beat > beat)
            .Select(entry => $"{entry.State} at {entry.Beat}");
        var pending = pendingBpm.Concat(pendingTransport).ToList();
        return pending.Count == 0 ? "none" : string.Join(", ", pending);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        if (_syncClient is not null)
        {
            _syncClient.SnapshotChanged -= OnSnapshotChanged;
            _syncClient.Dispose();
        }
    }
}
