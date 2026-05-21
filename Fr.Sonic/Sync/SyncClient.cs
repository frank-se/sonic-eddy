using Fr.Sonic.Model.Objects;
using Fr.Sonic.Registries.Nodes;

namespace Fr.Sonic.Sync;

public sealed class SyncClient
{
    private const string SyncMasterNodeName = "se.sync_master";

    private readonly NodeRegistry _nodeRegistry;
    private readonly object _lock = new();
    private SyncSnapshot? _snapshot;

    internal SyncClient(NodeRegistry nodeRegistry)
    {
        _nodeRegistry = nodeRegistry;
        _nodeRegistry.Added += OnNodeAdded;
        _nodeRegistry.Updated += OnNodeUpdated;
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

    internal void AttachExistingNodes()
    {
        foreach (var node in _nodeRegistry.Objects)
            AttachNode(node);
    }

    private void OnNodeAdded(Node node) => AttachNode(node);

    private void OnNodeUpdated(Node node, NodeChangeType changeType)
    {
        if (changeType == NodeChangeType.Params)
            AttachNode(node);
    }

    private void AttachNode(Node node)
    {
        if (node.Name != SyncMasterNodeName)
            return;

        _ = UpdateFromNodeAsync(node);
    }

    private async Task UpdateFromNodeAsync(Node node)
    {
        var parameters = await node.Params.ConfigureAwait(false);

        SyncSnapshot? previous;
        lock (_lock)
            previous = _snapshot;

        if (!SyncSnapshotParser.TryParse(
                node.ObjectId,
                node.ObjectSerial,
                parameters,
                previous?.BeatHistory ?? [],
                out var snapshot))
            return;

        lock (_lock)
            _snapshot = snapshot;

        SnapshotChanged?.Invoke(snapshot);
    }
}
