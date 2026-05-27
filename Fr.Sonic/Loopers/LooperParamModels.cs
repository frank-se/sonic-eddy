using System.Text.Json.Serialization;

namespace Fr.Sonic.Loopers;

public sealed record LooperParams(
    float? Mix,
    IReadOnlyList<LooperCommand> Commands,
    LooperState? State);

public sealed record LooperCommand(
    ulong Beat,
    string Command);

public sealed record LooperState(
    [property: JsonPropertyName("version")]
    int Version,
    [property: JsonPropertyName("active_loop")]
    uint? ActiveLoop,
    [property: JsonPropertyName("loops")]
    IReadOnlyList<LoopState> Loops,
    [property: JsonPropertyName("recording")]
    bool Recording,
    [property: JsonPropertyName("transport_alignment")]
    TransportAlignment TransportAlignment,
    [property: JsonPropertyName("active_playback")]
    ActivePlayback? ActivePlayback,
    [property: JsonPropertyName("pending_jobs")]
    IReadOnlyList<PendingJob> PendingJobs,
    [property: JsonPropertyName("last_command_failure")]
    LastCommandFailure? LastCommandFailure);

public sealed record LoopState(
    [property: JsonPropertyName("loop_number")]
    uint LoopNumber,
    [property: JsonPropertyName("generation")]
    ulong Generation,
    [property: JsonPropertyName("state")]
    string State,
    [property: JsonPropertyName("source")]
    string Source,
    [property: JsonPropertyName("start_beat")]
    ulong? StartBeat,
    [property: JsonPropertyName("end_beat")]
    ulong? EndBeat,
    [property: JsonPropertyName("length_beats")]
    ulong? LengthBeats,
    [property: JsonPropertyName("length_frames")]
    ulong LengthFrames,
    [property: JsonPropertyName("sample_rate")]
    uint SampleRate,
    [property: JsonPropertyName("channels")]
    uint Channels,
    [property: JsonPropertyName("bpm")]
    double? Bpm);

public sealed record TransportAlignment(
    [property: JsonPropertyName("transport_start_beat")]
    ulong? TransportStartBeat,
    [property: JsonPropertyName("ring_buffer_zero_beat")]
    ulong? RingBufferZeroBeat);

public sealed record ActivePlayback(
    [property: JsonPropertyName("loop_number")]
    uint LoopNumber,
    [property: JsonPropertyName("generation")]
    ulong Generation,
    [property: JsonPropertyName("started_at_beat")]
    ulong? StartedAtBeat,
    [property: JsonPropertyName("playhead_samples")]
    ulong PlayheadSamples);

public sealed record PendingJob(
    [property: JsonPropertyName("kind")]
    string Kind,
    [property: JsonPropertyName("loop_number")]
    uint? LoopNumber,
    [property: JsonPropertyName("generation")]
    ulong? Generation);

public sealed record LastCommandFailure(
    [property: JsonPropertyName("beat_number")]
    ulong BeatNumber,
    [property: JsonPropertyName("command")]
    string Command,
    [property: JsonPropertyName("reason")]
    string Reason);
