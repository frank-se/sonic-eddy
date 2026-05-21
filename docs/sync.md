# Sync Protocol

Sonic Eddy uses a beat schedule published via `SPA_PROP_params` on a dedicated
PipeWire node to synchronize tempo and beat position across nodes. Any node —
including those in a different PipeWire clock domain — can consume the schedule
and achieve near-sample-accurate sync without a continuous stream.

## Beat Schedule

The sync signal is a small lookahead table of upcoming beats.

**Published in:** `SPA_PROP_params` JSON field `beat.schedule`

**Value:** JSON array of `[beat, nsec]` pairs

```json
[
  [42, 1234567890000],
  [43, 1234568390000],
  [44, 1234568890000],
  [45, 1234569390000]
]
```

### Fields

| Field  | Type     | Description                                                                     |
| ------ | -------- | ------------------------------------------------------------------------------- |
| `beat` | `uint64` | Absolute beat count from session start. Monotonically increasing, never resets. |
| `nsec` | `uint64` | Linux `CLOCK_MONOTONIC` time, in nanoseconds, at which that beat occurs.        |

The sync clock is the host OS monotonic clock, not a PipeWire graph or stream
clock. The master can read it without processing audio, and consumers in
different PipeWire clock domains can compare schedule entries against the same
system-wide monotonic time base.

### Current beat

`current_beat` is the latest beat whose scheduled `nsec` is less than or equal
to `now_nsec`:

```text
current_beat = max(beat where beat.nsec <= now_nsec)
```

If `now_nsec` falls between two scheduled beats, `current_beat` is the earlier
beat. All lead-time validation rules use this integer value.

Consumers cache the latest snapshot from the master. Since `beat.schedule` is a
lookahead window and may contain only upcoming beats, consumers that need
`current_beat` derive it from cached beat history and the latest snapshot, not
only from the currently published schedule entries.

### Lookahead

The schedule holds the next N upcoming beats. Default N is 4. The window slides
forward as beats pass.

### Update triggers

- When the number of entries whose beat time is in the past reaches M. Default M
  is 2. At faster tempos beats pass more quickly, so this naturally produces
  more frequent updates.
- Immediately when tempo changes — the new schedule supersedes the old one.

N and M are configured at sync-master startup. Changing them requires restarting
the master.

## Transport Parameters

Published alongside the beat schedule so consumers can fully interpret the sync
signal without needing a timeline or external state.

**Published in:** `SPA_PROP_params` JSON field `beat.params`

**Value:** JSON object with two arrays of `[beat, value]` tuples

```json
{
  "bpm": [
    [0, 120.0],
    [48, 144.0]
  ],
  "transport_state": [[0, "playing"]]
}
```

Each entry is valid from its beat onwards until superseded by the next entry in
the array. Under normal operation both arrays contain a single entry. A
scheduled tempo or state change appears as a second entry with a future beat.

The beat schedule is generated from the full `bpm` array, not just the currently
active BPM. If a future BPM entry falls inside the lookahead window, timestamps
before that entry use the previous BPM and timestamps from that entry onward use
the new BPM. The scheduled beat itself is the boundary: its timestamp is reached
using the previous tempo, and the interval from that beat to the next beat uses
the new tempo.

### Fields

| Field             | Type                       | Description                                            |
| ----------------- | -------------------------- | ------------------------------------------------------ |
| `bpm`             | `[[uint64, float64], ...]` | Tempo changes. Each entry: `[valid_from_beat, bpm]`.   |
| `transport_state` | `[[uint64, string], ...]`  | State changes. Each entry: `[valid_from_beat, state]`. |

### Transport states

| State             | Meaning                                                                                          |
| ----------------- | ------------------------------------------------------------------------------------------------ |
| `stopped`         | No playback. Consumers should not act on the beat schedule.                                      |
| `start_scheduled` | Playback will begin at the entry's `valid_from_beat`. Consumers prepare and fire together on it. |
| `playing`         | Playback is running. The entry's `valid_from_beat` is the beat playback began at.                |

To determine when playback started or is scheduled to start, inspect the
`transport_state` array — the `valid_from_beat` of the relevant entry is that
beat.

### Cleanup

On each update, entries are removed when they have been superseded by a later
entry AND their beat is older than the oldest beat remaining in the schedule.
Each array always retains at least one entry (the currently active value).

For `transport_state`, cleanup always retains the currently active transport
entry even if its beat is older than the oldest beat in the schedule. Future
scheduled transport entries are also retained until they become active and are
later superseded. Older superseded entries may be discarded. This ensures
late-joining consumers can determine the current transport state and, when
playing, the beat at which playback began.

Beats always progress monotonically regardless of transport state. The schedule
tells consumers _when_ each beat fires in wall-clock time; transport parameters
tell them _whether_ and _where_ to act on it.

`SPA_PROP_params` is republished atomically whenever either field changes —
tempo change, transport state change, or schedule slide — so consumers always
see a consistent snapshot of both fields.

## Sync Master

The sync master is a PipeWire node with no ports whose sole responsibility is
maintaining the beat clock and publishing sync data.

### Node identity

The node is registered with a well-known name so consumers can locate it without
additional discovery logic:

```
node.name = se.sync_master
```

Only one instance should exist at a time. A second instance detecting the name
already present should exit with an error.

### Published data

Both fields are written together as a single JSON string into `SPA_PROP_params`
on `SPA_PARAM_Props`:

```json
{
  "beat.schedule": [
    [42, 1234567890000],
    [43, 1234568390000]
  ],
  "beat.params": {
    "bpm": [
      [0, 120.0],
      [48, 144.0]
    ],
    "transport_state": [[0, "playing"]]
  }
}
```

The update is atomic — consumers always observe both fields consistent with each
other.

### Beat clock

An internal monotonic beat counter increments independently of transport state.
The master schedules a timer on the PipeWire loop that fires when
`entries_older_than_now >= M`. On each timer fire it slides the schedule window
forward by computing the next N beat timestamps from Linux `CLOCK_MONOTONIC`
and the piecewise tempo map in `beat.params.bpm`, then republishes
`SPA_PROP_params`.

### Update triggers

1. **M-beat timer** — timer fires when M schedule entries have passed; schedule
   slides and `SPA_PROP_params` is republished.
2. **Tempo change** — a new BPM invalidates every timestamp at or after its
   `valid_from_beat`; the schedule is recomputed from the current beat using the
   updated piecewise tempo map and republished immediately.
3. **Transport state change** — `beat.params` is updated and republished
   immediately together with the current schedule.

All updates happen on the PipeWire main loop thread, never on an RT thread.

### Controlling the master

Clients change BPM or transport state by sending a `SPA_PARAM_Props` update to
the master node via `pw_node_set_param`. The payload follows the same
`[beat, value]` tuple format as the published data and is treated like an HTTP
PATCH request: only the fields present in the payload are considered; omitted
fields are left unchanged. The master is authoritative — it merges the incoming
values with current state, validates them, and either applies and republishes or
silently ignores. There is no acknowledgement; clients observe the outcome on
the next `SPA_PROP_params` update.

The master enforces a minimum lead-time rule before accepting a change:

- **Stopped** — immediate changes (any beat ≤ current beat) are accepted.
- **Playing or start_scheduled** — only changes with
  `valid_from_beat ≥ current_beat + K` are accepted. Default K is 4. This
  ensures all consumers have enough schedule lookahead to react before the
  change takes effect.

K is configured at sync-master startup alongside N and M.

An immediate change sends the current beat as the validity anchor; a scheduled
future change sends the target beat:

```json
{ "beat.params": { "bpm": [[48, 144.0]] } }
```

### Consumer library

`se_sync_consumer_create` watches the PipeWire registry for a node named
`se.sync_master`. When found it subscribes to `SPA_PARAM_Props` via
`pw_node_subscribe_params`. Incoming param events parse `SPA_PROP_params` and
update the internal cache. If the master node disappears the consumer holds the
last known state until a new master appears.

## C Interface

### Types

```c
typedef enum {
    SE_SYNC_STOPPED,
    SE_SYNC_START_SCHEDULED,
    SE_SYNC_PLAYING,
} se_sync_transport_state_t;

typedef struct {
    uint64_t beat;
    double   bpm;
} se_sync_bpm_entry_t;

typedef struct {
    uint64_t                  beat;
    se_sync_transport_state_t state;
} se_sync_state_entry_t;

typedef struct {
    se_sync_bpm_entry_t   *bpm;         /* owned by consumer; do not free */
    int                    bpm_count;
    se_sync_state_entry_t *state;       /* owned by consumer; do not free */
    int                    state_count;
} se_sync_params_t;

typedef struct {
    uint64_t beat;
    uint64_t nsec;
    int64_t  sample;      /* samples from now_nsec; negative = already passed */
    double   sample_frac; /* sub-sample offset [0.0, 1.0) */
} se_sync_beat_t;

typedef struct {
    se_sync_params_t  params;
    se_sync_beat_t   *beats;     /* owned by the consumer; do not free */
    int               beat_count;
} se_sync_result_t;
```

### Functions

```c
/* Find the sync master node by well-known name and subscribe to its SPA_PROP_params. */
se_sync_consumer_t *se_sync_consumer_create(
    struct pw_loop     *loop,
    struct pw_registry *registry
);

void se_sync_consumer_destroy(se_sync_consumer_t *consumer);

/*
 * Write current params and beats into *out.
 * now_nsec is Linux CLOCK_MONOTONIC time for the current process cycle.
 * sample_rate is the consumer node's sample rate.
 * *out points to internally managed memory; do not free it.
 * The pointer is valid until the next call to se_sync_get_beats or
 * se_sync_consumer_destroy. Safe to call from a soft real-time callback —
 * no allocation occurs on the hot path.
 * Returns the number of beats in (*out)->beats.
 */
int se_sync_get_beats(
    se_sync_consumer_t  *consumer,
    uint64_t             now_nsec,
    uint32_t             sample_rate,
    se_sync_result_t   **out
);
```

The consumer stores incoming sync data as immutable snapshots. The
`SPA_PROP_params` callback parses the update, builds a complete snapshot, and
publishes it with `std::atomic<std::shared_ptr<const SyncSnapshot>>`.
`se_sync_get_beats` atomically loads the current snapshot, writes the computed
sample offsets into caller-stable result storage, and returns pointers into that
storage via `*out`. It never parses JSON or allocates on the hot path.
`(*out)->params` is always populated regardless of beat count.

Result storage is sized from the startup-configured lookahead limit. If a later
snapshot exceeds the current storage capacity, storage is grown on the
`SPA_PROP_params` callback path before that snapshot is published, never inside
`se_sync_get_beats`.

This design intentionally uses the standard library atomic shared-pointer
specialization rather than Boost. The target platform is amd64 Linux; the first
implementation does not require a stricter lock-free guarantee.

Snapshot reclamation happens off the process callback. Snapshots use a custom
deleter that places retired snapshot pointers into a fixed-capacity reclamation
queue; the queue is drained on the `SPA_PROP_params` callback path or during
consumer destruction. `se_sync_get_beats` may load, retain, and release
`shared_ptr` references, but the final snapshot delete/free is deferred away
from the hot path.

### Requesting changes

```c
/*
 * Request a BPM change at the given beat. Fire-and-forget — the master may
 * accept or ignore the request. Observe the outcome via se_sync_get_beats.
 */
void se_sync_request_bpm(
    se_sync_consumer_t *consumer,
    uint64_t            at_beat,
    double              bpm
);

/*
 * Request a transport state change at the given beat. Fire-and-forget — the
 * master may accept or ignore the request. Observe the outcome via se_sync_get_beats.
 */
void se_sync_request_transport_state(
    se_sync_consumer_t        *consumer,
    uint64_t                   at_beat,
    se_sync_transport_state_t  state
);
```

Both functions send a partial `SPA_PROP_params` update (PATCH semantics) to the
sync master via `pw_node_set_param`. The consumer already holds the master node
proxy, so no additional setup is needed. Results are observed on the next
`se_sync_get_beats` call.

## Cross-graph sync

PipeWire nodes in different clock domains cannot share sample-accurate ticks
directly. A consumer node reads Linux `CLOCK_MONOTONIC` during its process
callback and converts a scheduled beat timestamp to its local sample position
using:

```text
local_sample = current_sample + (beat_nsec - now_nsec) * sample_rate / 1_000_000_000
```

This is as close to sample-accurate as possible without sharing a driver chain.

## MIDI Clock Converter

A PipeWire filter node with one MIDI output port that translates the sync
protocol into MIDI 1 clock messages. No MIDI time code, no Song Position Pointer
— the sync protocol has no timeline position, only a live beat clock, so those
concepts do not apply.

### Node

A `pw_filter` with a single MIDI output port and a `se_sync_consumer_t`
internally. In each process callback it calls `se_sync_get_beats` and emits MIDI
events at the computed sample positions.

### Clock pulses

MIDI clock runs at 24 PPQN. Given beat B at sample `S_B` and beat B+1 at sample
`S_{B+1}`, the 24 pulses are evenly spaced:

```text
pulse_sample[i] = S_B + i * (S_{B+1} - S_B) / 24   (i = 0..23)
```

The node tracks the current beat and pulse index across process callbacks,
emitting `0xF8` (MIDI Clock) at each computed sample. Tempo changes require no
special handling — the new beat timestamps already encode the new pulse spacing.

### Transport messages

`start_beat` is always the downbeat of a new playback, not a position in a
longer timeline. Every start is a fresh start; Continue has no meaning in this
model.

| Event                                                    | Message      |
| -------------------------------------------------------- | ------------ |
| `transport_state` becomes `start_scheduled` or `playing` | `0xFA` Start |
| `transport_state` becomes `stopped`                      | `0xFC` Stop  |

The Start message is emitted once per `start_beat`, at that beat's sample
position. A converter that observes both `start_scheduled` and the later
`playing` state for the same `start_beat` must not emit a second Start. The Stop
message is emitted immediately on the first process callback where the state is
observed as `stopped`.

## C# API

The C# wrapper follows the same P/Invoke pattern as `Fr.Wireplumber` and
`Fr.Pw.Monitoring`. A static facade class wraps `se_sync_consumer_t` and exposes
a typed, reactive surface to the UI layer.

### Change notification

The wrapper needs to know when `SPA_PROP_params` changes so it can raise a C#
event. Internally it registers a C-level callback with the native library at
initialisation time. This callback is an implementation detail of the wrapper —
it is not part of the public C API — and its design is driven by P/Invoke
constraints rather than general C API ergonomics:

- The callback must be a `static` method annotated with `[UnmanagedCallersOnly]`
  to avoid a managed delegate allocation that the GC could collect.
- Its signature is restricted to blittable types only.
- It fires on the PipeWire main loop thread, not a managed thread, so any
  interaction with managed state requires care.

The callback captures two timestamps at the moment it fires:

| Field               | Type    | Description                                                              |
| ------------------- | ------- | ------------------------------------------------------------------------ |
| `monotonic_nsec`    | `ulong` | Linux `CLOCK_MONOTONIC` time at which the param update arrived.          |
| `arrival_timestamp` | `long`  | `Stopwatch.GetTimestamp()` captured in the callback, on the same thread. |

Pairing both clocks at a single instant gives the C# layer a stable correlation
point. After that, beat timestamps from `se_sync_get_beats` can be converted to
C# monotonic time without further clock synchronisation.

### Public surface

```csharp
public record SyncBpmEntry(ulong Beat, double Bpm);

public record SyncStateEntry(ulong Beat, SyncTransportState State);

public record SyncParams(
    SyncBpmEntry[]   Bpm,
    SyncStateEntry[] TransportState
);

public record SyncParamsChangedArgs(
    SyncParams Params,
    ulong      MonotonicNsec,
    long       ArrivalTimestamp
);

public enum SyncTransportState { Stopped, StartScheduled, Playing }
```

```csharp
public static class FrPwSync
{
    public static void Start(/* pw_loop, pw_registry handles */);
    public static void Stop();

    public static event EventHandler<SyncParamsChangedArgs> ParamsChanged;

    public static void RequestBpm(ulong atBeat, double bpm);
    public static void RequestTransportState(ulong atBeat, SyncTransportState state);
}
```

`ParamsChanged` is raised on the PipeWire main loop thread. Subscribers that
touch UI state must marshal to the UI thread in the usual way.
