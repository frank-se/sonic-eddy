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
| `nsec` | `uint64` | PipeWire monotonic clock time (nanoseconds) at which that beat occurs.          |

### Lookahead

The schedule holds the next N upcoming beats. Default N is 4. The window slides
forward as beats pass.

### Update triggers

- When the number of entries whose beat time is in the past reaches M. Default
  M is 2. At faster tempos beats pass more quickly, so this naturally produces
  more frequent updates.
- Immediately when tempo changes — the new schedule supersedes the old one.

Both N and M are runtime-configurable so update frequency can be tuned without
restarting.

## Transport Parameters

Published alongside the beat schedule so consumers can fully interpret the sync
signal without needing a timeline or external state.

**Published in:** `SPA_PROP_params` JSON field `beat.params`

**Value:** JSON object with two arrays of `[beat, value]` tuples

```json
{
  "bpm":             [[0, 120.0], [48, 144.0]],
  "transport_state": [[0, "playing"]]
}
```

Each entry is valid from its beat onwards until superseded by the next entry in
the array. Under normal operation both arrays contain a single entry. A scheduled
tempo or state change appears as a second entry with a future beat.

### Fields

| Field             | Type                          | Description                                                        |
| ----------------- | ----------------------------- | ------------------------------------------------------------------ |
| `bpm`             | `[[uint64, float64], ...]`    | Tempo changes. Each entry: `[valid_from_beat, bpm]`.               |
| `transport_state` | `[[uint64, string], ...]`     | State changes. Each entry: `[valid_from_beat, state]`.             |

### Transport states

| State             | Meaning                                                                                              |
| ----------------- | ---------------------------------------------------------------------------------------------------- |
| `stopped`         | No playback. Consumers should not act on the beat schedule.                                          |
| `start_scheduled` | Playback will begin at the entry's `valid_from_beat`. Consumers prepare and fire together on it.     |
| `playing`         | Playback is running. The entry's `valid_from_beat` is the beat playback began at.                    |

To determine when playback started or is scheduled to start, inspect the
`transport_state` array — the `valid_from_beat` of the relevant entry is that
beat.

### Cleanup

On each update, entries are removed when they have been superseded by a later
entry AND their beat is older than the oldest beat remaining in the schedule.
Each array always retains at least one entry (the currently active value).

Beats always progress monotonically regardless of transport state. The schedule
tells consumers _when_ each beat fires in wall-clock time; transport parameters
tell them _whether_ and _where_ to act on it.

`SPA_PROP_params` is republished atomically whenever either field changes — tempo
change, transport state change, or schedule slide — so consumers always see a
consistent snapshot of both fields.

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
  "beat.schedule": [[42, 1234567890000], [43, 1234568390000]],
  "beat.params": {
    "bpm":             [[0, 120.0], [48, 144.0]],
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
forward by computing the next N beat timestamps from the current BPM and the
PipeWire monotonic clock, then republishes `SPA_PROP_params`.

### Update triggers

1. **M-beat timer** — timer fires when M schedule entries have passed; schedule
   slides and `SPA_PROP_params` is republished.
2. **Tempo change** — new BPM invalidates all future timestamps; schedule is
   recomputed from the current beat and republished immediately.
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
silently ignores. There is no acknowledgement; clients observe the outcome on the
next `SPA_PROP_params` update.

The master enforces a minimum lead-time rule before accepting a change:

- **Stopped** — immediate changes (any beat ≤ current beat) are accepted.
- **Playing or start_scheduled** — only changes with `valid_from_beat ≥
  current_beat + K` are accepted. Default K is 4. This ensures all consumers
  have enough schedule lookahead to react before the change takes effect.

K is runtime-configurable alongside N and M.

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
 * now_nsec is the PipeWire monotonic clock time for the current process cycle.
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

All internal buffers (beat schedule, bpm entries, state entries) are allocated at
consumer creation and reallocated only on the `SPA_PROP_params` callback path,
never inside `se_sync_get_beats`. `(*out)->params` is always populated regardless
of beat count.

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
directly. A consumer node converts a scheduled beat time stamp to its local
sample position using:

```text
local_sample = current_sample + (beat_nsec - now_nsec) * sample_rate / 1_000_000_000
```

This is as close to sample-accurate as possible without sharing a driver chain.
