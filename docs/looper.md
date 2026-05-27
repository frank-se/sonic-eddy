# Looper

Every channel and group channel has a looper inserted after the input playback
node, and one looper inserted after the output playback node, effectively
allowing the user to record loops, either dry, or wet, i.e. before or after the
optional filter chain.

The looper can record or play back, and is synchronized with the sync node
described in the sync spec sync.md.

The looper provides two nodes, a capture and a playback node. The capture node
captures audio, and the playback node plays it.

## Synchronization

The looper synchronizes with the time master using the `beat.schedule` and
`beat.params` values published by the sync node described in sync.md.

The `beat.schedule` is the authoritative timing source. In each processing
cycle, the looper reads the current sync snapshot and converts scheduled beat
timestamps into sample offsets in the current audio buffer. The conversion uses
the process timestamp, the schedule entry's monotonic timestamp, and the current
sample rate.

`beat.params.bpm` is used as loop metadata and to detect tempo changes that
affect stored loops. Beat placement is not derived from a fixed number of
samples per beat calculated from BPM.

The monotonic beat numbers from the time master are used for all looper command
scheduling and loop boundaries.

## Recording Buffer

The looper maintains a recording buffer. The buffer stores samples. The buffer
is allocated at the start of the looper, and is fixed after. It is organized as
a ring buffer.

The recording buffer duration is configurable when the looper is created. The
initial implementation should default to five minutes, which is expected to be
more than enough for normal use.

The ring buffer defines the maximum retained recording history. A cut command is
accepted only if the full requested beat range is still present in the recording
buffer when the command is processed. If the requested range is older than the
retained history, the looper rejects the cut and leaves the existing loop slot
unchanged.

## Recording

The looper records continuously while the time master's transport state is
`playing`.

When the looper observes a `start_scheduled` transport state, it arms recording
and starts writing ring buffer position `0` exactly at the scheduled start beat.
When the looper observes an already active `playing` transport state, it aligns
the ring buffer to the transport state's beat tuple, so ring buffer position `0`
corresponds to the beat where playback began.

When the looper observes a `stopped` transport state, it stops recording and
stops active loop playback.

## Stored Loops

The looper can store 10 loops of different length internally. Commands refer to
loops by `loop_number`, which is an internal slot identifier. The looper does
not require loop numbers to be presented directly in the UI, and the UI may map
its own controls or labels to these slot identifiers.

Each loop slot stores a loop descriptor. The descriptor contains the loop
number, generation, start beat, end beat, length in samples, channel count,
sample rate, BPM at the time of the cut, playback state, and the location of
the loop audio. For ring-buffer-backed loops, the descriptor also stores the
absolute recording sample range used by the loop.

Loop descriptors are immutable once published to the processing loop. Replacing
or clearing a loop slot publishes a new descriptor for that slot. The old
descriptor and any memory referenced by it are retired by the background thread
after the processing loop can no longer reference them.

Loop audio can live in one of two places:

- In the recording ring buffer.
- In owned loop memory allocated for the loop slot.

### Cutting a Loop

The process of creating a loop is called cutting. The looper cuts based on the
`cut` command. The `cut` command has either two or three parameters, and is used
to store a new loop in the looper memory.

The looper copies the contents of the loop asynchronously in the background.
Playback of a loop is possible immediately when the `cut` command has been
processed, even if the data is not copied yet. The looper takes the data
directly from the ring buffer if necessary.

When a cut command is executed, the processing loop validates that the requested
beat range is still present in the recording ring buffer. If it is valid, the
processing loop publishes a new loop descriptor that points at the corresponding
ring buffer range and enqueues a background copy job. This makes the loop
available for playback immediately without copying audio on the real-time path.

The background thread copies the loop audio into owned loop memory. When the
copy is complete, it publishes a replacement descriptor for the same loop slot
that points at the owned memory. Playback keeps using whichever descriptor was
current at the beginning of the process cycle.

- `cut <start_beat> <end_beat> <loop_number>`
- `cut <loop_length> <loop_number>`

The explicit beat range is inclusive. For example, beats `0` to `3` are the
first four beats.

For the explicit beat range form, `start_beat` must be less than or equal to
`end_beat`. For the loop length form, `loop_length` must be greater than zero.
The `loop_number` must refer to one of the looper's 10 stored loop slots.

The loop length form is relative to the beat where the command is scheduled.
For a command scheduled at beat `B`, `cut <loop_length> <loop_number>` cuts the
range ending immediately before `B`. It is equivalent to:

```text
cut <B - loop_length> <B - 1> <loop_number>
```

### Playback Loop Memory

Owned loop memory is allocated outside the processing loop. The processing loop
never allocates or frees memory for stored loops.

Each loop slot has one published descriptor at a time. The descriptor points
either to a stable range in the recording ring buffer or to owned loop memory.
The descriptor includes a generation number so background work for an older cut
cannot overwrite a newer cut in the same slot.

Ring-buffer-backed playback is allowed to continue using the descriptor that was
published by the successful cut. After a cut has been accepted, the looper does
not reject playback merely because the original recording range is now older
than the retained recording history.

Replacing, archiving, or clearing a loop slot does not free the previous loop
memory immediately. Previous descriptors and owned buffers are put on a retired
list. The background thread frees retired memory only after it is known that the
processing loop has advanced past any process cycle that could have loaded the
old descriptor.

### Archiving Stored Loops

Archiving copies the loop data into a file and frees the memory to be used
again.

- `archive <loop_number>`

Archive files are written under Sonic Eddy's local app data directory. The
archive root is provided by the looper creation config as
`archive_folder_path`. If no archive folder path is configured, archive commands
are rejected until the client creates a looper with an explicit archive folder.

The archive file must contain the loop audio and enough metadata to restore or
inspect the loop later: loop number, generation, start beat, end beat, length in
samples, sample rate, channel count, and BPM at cut time. The exact file format
must be versioned.

The archive audio format is FLAC. FLAC is widely used, has good encoding and
decoding performance, preserves PCM audio without loss, supports embedded
metadata, and has suitable library licensing for Sonic Eddy. If a different
container is chosen later, it must still be lossless and must preserve the same
metadata.

## Playback

Start playback of loop with `loop_number`.

- `play <loop_number>`

The looper starts playback exactly at the beat where the `play` command is
scheduled. It does not implicitly align playback to a bar, loop boundary, or any
other musical grid. If aligned playback is desired, the client schedules the
`play` command at the aligned beat.

Only one loop can play at a time. Starting playback for a loop stops any
previously playing loop and makes the requested loop the active playback loop.
The looper does not mix multiple stored loops together.

- `stop`

The `stop` command stops the active playback loop. If no loop is playing, it has
no effect.

### Time Stretching

Time stretching is not currently implemented. If the time master changes tempo,
all currently stored loops will be archived.

## Processing Loop

The looper exposes a capture and playback node for PipeWire and WirePlumber
routing, but internally there is a single processing function for the looper.
That processing function handles input capture, loop recording, command
execution, loop playback, and output generation in one place.

For each process cycle, the looper:

1. Reads input samples from the capture side.
2. Reads the current sync snapshot and maps scheduled beat timestamps to sample
   offsets in the current audio buffer.
3. Applies transport changes that occur within the current buffer.
4. Drains pending param events from the param event queue.
5. Writes input samples into the recording ring buffer when the transport is
   playing.
6. Executes due commands at their scheduled beat sample offset.
7. Renders active loop playback for the current buffer.
8. Mixes input and loop playback according to `mix`.
9. Writes the result to the playback side.

The initial implementation supports the `mix = 0` case as live passthrough:
capture samples are copied to the playback side and any missing playback frames
are filled with silence. This provides a first end-to-end PipeWire graph test
before loop recording and loop playback are implemented.

The initial `mix` implementation applies the normal dry/wet formula, but the
wet loop playback signal is silence until loop playback exists:

```text
output = input * (1 - mix) + 0 * mix
```

The first loop implementation records into the fixed history buffer while the
sync transport is playing and supports both `cut <loop_length> <loop_number>`
and `cut <start_beat> <end_beat> <loop_number>`. Cut ranges are converted to
recording ring buffer sample ranges using the sync beat schedule.

If a beat boundary falls inside the current process buffer, all beat-scheduled
state changes for that beat take effect at the corresponding sample offset, not
at the beginning or end of the process cycle.

The processing loop must not allocate memory, parse JSON, copy archived loop
data, or take blocking locks on the real-time path. Param changes and background
copy/archive work prepare immutable state that the processing loop can consume
without blocking.

## Background Processing Thread

The looper owns a background processing thread for work that must not happen in
the processing loop. The background thread wakes when there is queued work and
sleeps otherwise.

The background thread handles:

1. Copying newly cut loops from the recording ring buffer into owned loop
   memory.
2. Writing archived loops to files from either owned loop memory or, if still
   valid, the recording ring buffer.
3. Publishing replacement loop descriptors after copy or archive work
   completes.
4. Retiring old loop descriptors and freeing owned loop memory.
5. Applying non-real-time PipeWire param updates requested by the processing
   loop, such as removing processed commands from the published params.
6. Reporting completion or failure state through the looper params/status.

The processing loop enqueues background jobs when it executes commands that
require non-real-time work, such as `cut` and `archive`. Jobs include the loop
number and generation they apply to. Before publishing any result, the
background thread verifies that the target slot still has the same generation.
If the slot has been replaced, the job result is discarded and any temporary
memory is retired.

Archive jobs publish an archived or empty descriptor only after the file write
has completed successfully. If archiving fails, the current loop descriptor
remains active and the failure is reported in status.

The background thread may allocate memory, copy audio, perform file I/O, and
parse or format status data. It must not block the processing loop. State shared
with the processing loop is exchanged by publishing immutable descriptors or by
using fixed-size queues suitable for real-time producers.

If a fixed-size queue used to hand work to the background thread is full, the
producer drops the job and logs an error. Queue overflow is considered an
implementation sizing problem; if it occurs in normal use, the queue capacity
must be increased.

## Control

The looper is controlled using `SPA_PROP_params` in the PipeWire props params
of the capture node. The same params are also the status surface for UI and
automation clients. There is no separate C or C# looper control API; clients
control the looper by requesting props params changes and observe the looper by
reading the published props params.

The props params store the following data:

- `commands`:
  - A JSON array of tuples (arrays with 2 elements)
  - Each element in the array contains `[beat_number, command]`
  - `beat_number` is an integer and indicates which beat number the `command`
    will be executed at
  - The combination of `beat_number` and `command` is the command identity. The
    same command must not appear more than once for the same beat, but different
    commands may be scheduled for the same beat.
  - The `command` is a string, the following values are valid:
    - `cut <start_beat> <end_beat> <loop_number>`
    - `cut <loop_length> <loop_number>`
    - `play <loop_number>`
    - `archive <loop_number>`
    - `stop`
  - `stop` stops the active loop playback.
- `mix`:
  - `mix` is a float, 0 <= mix <= 1, which describes how much of the playback
    versus the input is sent to the outputs.
    - 0 means inputs only,
    - 1 means playback signal only.
- `looper.state`:
  - A read-only JSON string describing the current looper state.
  - The looper publishes a new value when the state changes, not every process
    cycle.
  - The JSON contains populated loop slots, the active loop, loop metadata, and
    lightweight diagnostics needed by the UI.

### Params Schema

The params payload is JSON-compatible and follows this shape:

All numeric fields are JSON numbers. `BeatNumber`, `LoopGeneration`,
`SampleCount`, `SampleRate`, and `ChannelCount` must be non-negative integers.
`LoopNumber` must be an integer matching a valid loop slot. `Bpm` and `mix` may
be fractional.

```typescript
type BeatNumber = number;
type LoopNumber = number;
type LoopGeneration = number;
type SampleCount = number;
type SampleRate = number;
type ChannelCount = number;
type Bpm = number;

type LooperState = {
  version: 1;
  active_loop: LoopNumber | null;
  loops: LoopState[];
  recording: boolean;
  transport_alignment: TransportAlignment;
  active_playback: ActivePlayback | null;
  pending_jobs: PendingJob[];
  last_command_failure: LastCommandFailure;
};
```

Commands are stored as `[beat_number, command]` tuples. The command string is
parsed by the looper.

```typescript
type LooperCommand = [BeatNumber, CommandText];

type CommandText =
  | `cut ${BeatNumber} ${BeatNumber} ${LoopNumber}`
  | `cut ${number} ${LoopNumber}`
  | `play ${LoopNumber}`
  | `archive ${LoopNumber}`
  | "stop";
```

The initial ingestion path accepts these params through the looper capture node
`SPA_PARAM_Props` / `SPA_PROP_params`:

- `mix`: float, double, or int. Values are clamped to `0 <= mix <= 1`.
- `commands`: string containing `[beat, "command"]` tuples, for example
  `[[0,"cut 4 0"],[0,"play 0"]]`.
- `command`: string containing one command, treated as scheduled beat `0`.

The param callback parses commands outside the processing loop and pushes fixed
command events into a `boost::lockfree::spsc_queue`. The processing loop drains
that queue at the start of each cycle. If the queue is full, the command is
dropped and an error is logged.

Example ad-hoc `pw-cli` calls:

```bash
pw-cli set-param <looper-capture-object-id> Props '{ params = [ "mix" 0.5 ] }'
pw-cli set-param <looper-capture-object-id> Props '{ params = [ "commands" "[[0,\"cut 4 0\"],[0,\"play 0\"]]" ] }'
```

The `looper.state` JSON string contains one element per populated loop slot.
Empty slots are omitted.

```typescript
type LoopState = {
  loop_number: LoopNumber;
  generation: LoopGeneration;
  state: "stopped" | "playing";
  source: "ring" | "owned" | "archived";
  start_beat: BeatNumber | null;
  end_beat: BeatNumber | null;
  length_beats: number | null;
  length_frames: SampleCount;
  sample_rate: SampleRate;
  channels: ChannelCount;
  bpm: Bpm | null;
};
```

The remaining `looper.state` fields contain looper-wide state and
implementation diagnostics needed by the UI.

```typescript
type TransportAlignment = {
  transport_start_beat: BeatNumber | null;
  ring_buffer_zero_beat: BeatNumber | null;
};

type ActivePlayback = {
  loop_number: LoopNumber;
  generation: LoopGeneration;
  started_at_beat: BeatNumber | null;
  playhead_samples: SampleCount;
};

type PendingJob = {
  kind: "copy" | "archive" | "retire" | "params-update";
  loop_number: LoopNumber | null;
  generation: LoopGeneration | null;
};

type LastCommandFailure = {
  beat_number: BeatNumber;
  command: string;
  reason: string;
} | null;

```

### Command Parsing Examples

Different commands may be scheduled for the same beat. This allows a client to
cut a loop and start playback of that loop at the same beat:

```json
[
  [0, "cut 4 8 1"],
  [0, "play 1"]
]
```

The same `[beat_number, command]` tuple must not appear more than once:

```json
[
  [0, "play 1"],
  [0, "play 1"]
]
```

The second example is rejected as a duplicate command.

Beat numbers are based on the monotonic beat count provided by the timing master
described in the sync.md spec.

Clients can request changes to the params here using the normal means of
changing props params provided by PipeWire. The looper handles the request, and
then either updates the params when it can fulfill the request, or it rejects
the request by keeping the values unchanged.

The UI derives all per-looper display state from the published params. The
native and C# layers use generic PipeWire params read/write support for looper
control after a looper exists.

## Param Change Event Handling

PipeWire param change callbacks are not handled directly by the processing
logic. The param callback parses the requested props params, validates the
request shape, updates the node's published params, and then enqueues a compact
event into a param event queue.

The param event queue is a single-producer/single-consumer queue, implemented
with `boost::lockfree::spsc_queue`. The PipeWire param callback is the producer.
The looper processing function is the consumer.

The ordering is important:

1. A client requests a props params change on the looper capture node.
2. The param callback parses the request.
3. The callback rejects the request if the resulting `commands` array contains
   duplicate `[beat_number, command]` tuples.
4. If the request can be accepted, the callback updates the node params first.
5. After the updated params are visible on the node, the callback pushes the
   corresponding event to the param event queue.
6. The processing loop drains the queue on the next process cycle and acts on
   events that are now visible in the node params.

The processing loop must not parse JSON or update PipeWire params directly.
Param events consumed by the processing loop are already normalized into fixed
size command/control records.

If the param event queue is full, the param callback drops the event and logs an
error. This should not happen during normal use; if it does, the queue capacity
must be increased.

When the processing loop consumes a param event, it either applies the event
immediately or records it as a scheduled command to be executed at the requested
beat. After a command has been processed, the processing loop enqueues a
background job requesting the published params to be updated, for example to
remove processed commands from the `commands` array or to update status fields.

The background thread performs those PipeWire param updates outside the
real-time path. If the background thread needs to publish a status change that
affects processing state, it does so by publishing immutable state or by
enqueueing a new fixed-size event rather than calling into the processing loop
directly.

## Lifecycle API

Looper creation and destruction are not handled through looper params, because a
looper must exist before it can publish params. Sonic Eddy exposes a small
lifecycle API in the native library and C# wrapper for creating and destroying
looper instances.

The lifecycle API only owns looper instances and their PipeWire capture/playback
nodes. Once a looper has been created, all looper control and status use the
props params described above.

Creating a looper creates both PipeWire streams:

- `<name>.capture`
- `<name>.playback`

Both streams are tagged with a shared looper tag and distinct capture/playback
purposes so the C# factory can wait for both nodes and return a complete looper
object, matching the loopback and filter-chain module pattern.

Destroying a looper stops the looper, destroys both streams, stops any
background worker owned by the looper, and releases loop memory after the
processing loop can no longer reference it.

### C API

```c
typedef struct {
    const char *name;
    const char *tag;
    const char *description;
    const char *capture_target_object;  /* optional, may be NULL */
    const char *playback_target_object; /* optional, may be NULL */
    const char *archive_folder_path;    /* optional, may be NULL */
    uint32_t    channels;
    uint32_t    max_record_seconds;
    float       mix;
} se_looper_config_t;

/*
 * Create and start a looper on Sonic Eddy's owned PipeWire loop.
 * Returns true on success and writes the native looper handle to out_handle.
 * Returns false if the looper cannot be created.
 *
 * All size_t values, including 0, are valid handles when returned through
 * out_handle. Handles are owned by the native looper registry and can be passed
 * back later to destroy the looper.
 */
bool se_looper_create(const se_looper_config_t *config, size_t *out_handle);

/*
 * Stop and destroy a looper created by se_looper_create.
 * Unknown handles are ignored.
 */
void se_looper_destroy(size_t looper_handle);
```

The initial implementation may expose these through the existing `frsonic_*`
export naming scheme instead of the exact names above, but the ownership and
arguments should match this shape. The native library owns looper memory and
maps handles to live looper instances internally. Since `0` is a valid handle,
success or failure must not be encoded in the handle value.

### C# API

```csharp
public sealed record LooperConfig(
    string Name,
    string Description,
    string? CaptureTargetObject,
    string? PlaybackTargetObject,
    string? ArchiveFolderPath,
    uint Channels,
    uint MaxRecordSeconds,
    float Mix);

public sealed record Looper(
    string Name,
    string Tag,
    ulong LooperHandle,
    ulong CaptureNodeObjectSerial,
    ulong PlaybackNodeObjectSerial);

public static class FrSonicLoopers
{
    public static Task<Looper> CreateAsync(LooperConfig config);
    public static void Destroy(Looper looper);
}
```

`CreateAsync` generates a tag, passes it to the native create call, and waits
until the capture and playback nodes with that tag have appeared in the node
registry. The returned `Looper` gives consumers the native `ulong` looper handle
and both PipeWire node serials together, like loopback and filter-chain
creation.

`Destroy` passes `Looper.LooperHandle` back to native code and removes the
looper from the managed registry.

## Test Tools

The looper is tested in a real PipeWire graph rather than by mocking
`process()`. The native test tools are built with fr-sonic and create ordinary
PipeWire streams that can be connected manually or through target-object
properties.

### Looper Node

`se-looper` creates one looper and keeps it alive until Enter is pressed or the
optional duration expires.

```bash
se-looper -n se.test_looper -t test-looper \
  -c <capture-target-object> \
  -p <playback-target-object>
```

Both `-c` and `-p` are optional. If either target is omitted, that side is
created without autoconnect so it can be linked manually.

### Signal Source

`se-signal` creates a playback stream that emits deterministic test audio.

```bash
se-signal -n se.test_signal -p <target-object> -m alternating
se-signal -n se.test_signal -p <target-object> -m constant --value 0.7
se-signal -n se.test_signal -p <target-object> -m sine -f 60 --value 0.8
```

The alternating signal emits one second at `--value` followed by one second at
`--high-value`, repeating. The default values are `0.7` and `0.9`.
With `--json`, node identity and window/total statistics are emitted as JSON
lines for automated comparisons.

### Recorder

`se-record` creates a capture stream and prints once-per-second RMS/peak/min/max
statistics for the first channel, plus total statistics on exit.

```bash
se-record -n se.test_record -c <target-object>
```

Like the other tools, the target is optional. Without a target, the recorder
node is created unconnected for manual patching. With `--json`, node identity
and window/total statistics are emitted as JSON lines.

### Passthrough Setup

`scripts/looper-passthrough-test.sh` starts a signal source, one looper, and a
recorder as a real PipeWire graph:

```bash
scripts/looper-passthrough-test.sh
```

The script connects `se-signal` to `<looper-name>.capture` and `se-record` to
`<looper-name>.playback`, runs both tools with JSON stats, compares signal
window `n` against record window `n + 1`, then prints the looper, signal, and
recorder logs. Environment variables such as `LOOPER_NAME`, `DURATION`,
`RECORD_DURATION`, `SIGNAL_MODE`, `SIGNAL_VALUE`, `SIGNAL_HIGH_VALUE`, `MIX`,
and `RMS_TOLERANCE` can override the default setup.

`scripts/looper-mix-test.sh` runs the same setup with `MIX=0.5` by default.
The validator expects record window `n + 1` to match signal window `n` scaled
by `1 - MIX`.

`scripts/looper-mix-change-test.sh` starts with `INITIAL_MIX=0`, changes the
looper capture node to `CHANGED_MIX=0.5` halfway through the signal run using
`pw-cli set-param`, skips the transition window, and validates the shifted
window RMS before and after the change. With equal time at `mix = 0` and
`mix = 0.5`, total RMS is expected to be about `79%` of the source RMS, while
average amplitude would be `75%`.

`scripts/looper-cut-play-test.sh` uses a constant source, waits for the looper
to accumulate history, sends `mix=1` and `[[0,"cut 1 0"],[0,"play 0"]]` through
`pw-cli set-param`, and validates that later recorder windows contain the looped
wet signal.
