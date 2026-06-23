using System.Text.Json;
using Fr.Sonic.Model.Params;

namespace Fr.Sonic.Sync;

internal static class SyncSnapshotParser
{
    private const string BeatScheduleKey = "beat.schedule";
    private const string BeatParamsKey = "beat.params";

    public static bool TryParse(
        ulong masterObjectId,
        ulong masterObjectSerial,
        Dictionary<string, IParameter>? parameters,
        IReadOnlyList<BeatScheduleEntry> beatHistory,
        out SyncSnapshot snapshot)
    {
        snapshot = new SyncSnapshot(
            masterObjectId,
            masterObjectSerial,
            [],
            [],
            [],
            [],
            []);

        if (parameters is null)
            return false;

        if (!TryGetString(parameters, BeatScheduleKey, out var scheduleJson) ||
            !TryGetString(parameters, BeatParamsKey, out var paramsJson))
            return false;

        var schedule = ParseSchedule(scheduleJson);
        var (bpm, transportStates, resetBeats) = ParseParams(paramsJson);
        var history = MergeHistory(beatHistory, schedule);

        snapshot = new SyncSnapshot(
            masterObjectId,
            masterObjectSerial,
            history,
            schedule,
            bpm,
            transportStates,
            resetBeats);
        return true;
    }

    private static bool TryGetString(
        Dictionary<string, IParameter> parameters,
        string key,
        out string value)
    {
        value = string.Empty;
        if (!parameters.TryGetValue(key, out var parameter))
            return false;
        if (parameter is not Parameter<string> stringParameter)
            return false;

        value = stringParameter.Value;
        return true;
    }

    private static IReadOnlyList<BeatScheduleEntry> ParseSchedule(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = new List<BeatScheduleEntry>();

        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.GetArrayLength() < 2)
                continue;
            result.Add(new(
                item[0].GetUInt64(),
                item[1].GetUInt64()));
        }

        result.Sort((a, b) => a.Beat.CompareTo(b.Beat));
        return result;
    }

    private static (
        IReadOnlyList<BpmEntry> Bpm,
        IReadOnlyList<TransportStateEntry> TransportStates,
        IReadOnlyList<ulong> ResetBeats)
        ParseParams(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var bpm = new List<BpmEntry>();
        var transportStates = new List<TransportStateEntry>();
        var resetBeats = new List<ulong>();

        if (root.TryGetProperty("bpm", out var bpmElement))
        {
            foreach (var item in bpmElement.EnumerateArray())
            {
                if (item.GetArrayLength() < 2)
                    continue;
                bpm.Add(new(item[0].GetUInt64(), item[1].GetDouble()));
            }
        }

        if (root.TryGetProperty("transport_state", out var stateElement))
        {
            foreach (var item in stateElement.EnumerateArray())
            {
                if (item.GetArrayLength() < 2)
                    continue;
                transportStates.Add(new(
                    item[0].GetUInt64(),
                    ParseTransportState(item[1].GetString())));
            }
        }

        if (root.TryGetProperty("reset_beats", out var resetElement))
        {
            foreach (var item in resetElement.EnumerateArray())
            {
                if (item.TryGetUInt64(out var beat))
                    resetBeats.Add(beat);
            }
        }

        bpm.Sort((a, b) => a.Beat.CompareTo(b.Beat));
        transportStates.Sort((a, b) => a.Beat.CompareTo(b.Beat));
        return (bpm, transportStates, resetBeats);
    }

    private static SyncTransportState ParseTransportState(string? state) =>
        state switch
        {
            "stopped" => SyncTransportState.Stopped,
            "start_scheduled" => SyncTransportState.StartScheduled,
            "playing" => SyncTransportState.Playing,
            _ => SyncTransportState.Stopped
        };

    private static IReadOnlyList<BeatScheduleEntry> MergeHistory(
        IReadOnlyList<BeatScheduleEntry> beatHistory,
        IReadOnlyList<BeatScheduleEntry> schedule)
    {
        var byBeat = new SortedDictionary<ulong, ulong>();
        foreach (var entry in beatHistory)
            byBeat[entry.Beat] = entry.Nsec;
        foreach (var entry in schedule)
            byBeat[entry.Beat] = entry.Nsec;

        return byBeat.Select(pair => new BeatScheduleEntry(pair.Key, pair.Value))
            .ToList();
    }
}
