namespace Fr.Sonic.Sync;

public enum SyncTransportState
{
    Stopped,
    StartScheduled,
    Playing
}

public readonly record struct BeatScheduleEntry(ulong Beat, ulong Nsec);

public readonly record struct BpmEntry(ulong Beat, double Bpm);

public readonly record struct TransportStateEntry(
    ulong Beat,
    SyncTransportState State);

public sealed record SyncSnapshot(
    ulong MasterObjectId,
    ulong MasterObjectSerial,
    IReadOnlyList<BeatScheduleEntry> BeatHistory,
    IReadOnlyList<BeatScheduleEntry> BeatSchedule,
    IReadOnlyList<BpmEntry> Bpm,
    IReadOnlyList<TransportStateEntry> TransportStates)
{
    public BeatScheduleEntry? CurrentBeat(ulong nowNsec)
    {
        BeatScheduleEntry? current = null;
        foreach (var entry in BeatHistory)
        {
            if (entry.Nsec > nowNsec)
                break;
            current = entry;
        }

        return current;
    }

    public SyncTransportState TransportStateAt(ulong beat)
    {
        var state = SyncTransportState.Stopped;
        foreach (var entry in TransportStates)
        {
            if (entry.Beat > beat)
                break;
            state = entry.State;
        }

        return state;
    }

    public double BpmAt(ulong beat)
    {
        var bpm = Bpm.Count > 0 ? Bpm[0].Bpm : 0.0;
        foreach (var entry in Bpm)
        {
            if (entry.Beat > beat)
                break;
            bpm = entry.Bpm;
        }

        return bpm;
    }
}
