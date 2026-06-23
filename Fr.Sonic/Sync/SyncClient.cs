using Fr.Sonic.Model.Objects;
using System.Globalization;
using Fr.Sonic.Model.Params;

namespace Fr.Sonic.Sync;

public sealed class SyncClient : IDisposable
{
    private readonly Node _masterNode;
    private readonly object _lock = new();
    private SyncSnapshot? _snapshot;

    public SyncClient(Node masterNode)
    {
        _masterNode = masterNode;
        _masterNode.ParamsChanged += OnParamsChanged;
        _ = UpdateFromNodeAsync();
    }

    public event Action<SyncSnapshot>? SnapshotChanged;

    public SyncSnapshot? Snapshot
    {
        get
        {
            lock (_lock)
                return _snapshot;
        }
    }

    public BeatScheduleEntry? CurrentBeat() =>
        Snapshot?.CurrentBeat(MonotonicClock.NowNsec());

    public SyncTransportState TransportState()
    {
        var snapshot = Snapshot;
        if (snapshot is null)
            return SyncTransportState.Stopped;

        var current = snapshot.CurrentBeat(MonotonicClock.NowNsec());
        return current is null
            ? SyncTransportState.Stopped
            : snapshot.TransportStateAt(current.Value.Beat);
    }

    public IReadOnlyList<BeatScheduleEntry> NextBeats(int count)
    {
        if (count <= 0)
            return [];

        var now = MonotonicClock.NowNsec();
        var snapshot = Snapshot;
        if (snapshot is null)
            return [];

        return snapshot.BeatSchedule
            .Where(entry => entry.Nsec > now)
            .Take(count)
            .ToList();
    }

    public bool ScheduleBpm(ulong beat, double bpm)
    {
        var snapshot = Snapshot;

        Fr.Sonic.FrSonicWireplumber.SetParams(snapshot?.MasterObjectId ??
                                             _masterNode.ObjectId,
            [new("beat.params",
                $$"""{"bpm":[[{{beat}},{{bpm.ToString(CultureInfo.InvariantCulture)}}]]}""")]);
        return true;
    }

    public bool ScheduleReset(ulong beat)
    {
        var snapshot = Snapshot;
        Fr.Sonic.FrSonicWireplumber.SetParams(snapshot?.MasterObjectId ??
                                             _masterNode.ObjectId,
            [new("beat.params", $$"""{"reset_beats":[{{beat}}]}""")]);
        return true;
    }

    public bool ScheduleTransportState(
        ulong beat,
        SyncTransportState transportState)
    {
        var snapshot = Snapshot;

        Fr.Sonic.FrSonicWireplumber.SetParams(snapshot?.MasterObjectId ??
                                             _masterNode.ObjectId,
            [new("beat.params",
                $$"""{"transport_state":[[{{beat}},"{{FormatTransportState(transportState)}}"]]}""")]);
        return true;
    }

    private static string FormatTransportState(SyncTransportState state) =>
        state switch
        {
            SyncTransportState.StartScheduled => "start_scheduled",
            SyncTransportState.Playing => "playing",
            _ => "stopped"
        };

    private void OnParamsChanged(Dictionary<string, IParameter>? parameters) =>
        UpdateFromParams(parameters);

    private async Task UpdateFromNodeAsync()
    {
        var parameters = await _masterNode.Params.ConfigureAwait(false);
        UpdateFromParams(parameters);
    }

    private void UpdateFromParams(Dictionary<string, IParameter>? parameters)
    {
        SyncSnapshot? previous;
        lock (_lock)
            previous = _snapshot;

        if (!SyncSnapshotParser.TryParse(
                _masterNode.ObjectId,
                _masterNode.ObjectSerial,
                parameters,
                previous?.BeatHistory ?? [],
                out var snapshot))
            return;

        lock (_lock)
            _snapshot = snapshot;

        SnapshotChanged?.Invoke(snapshot);
    }

    public void Dispose()
    {
        _masterNode.ParamsChanged -= OnParamsChanged;
    }
}
