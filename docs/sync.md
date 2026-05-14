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

**Value:** JSON object

```json
{
  "bpm": 120.0,
  "transport_state": "start_scheduled",
  "start_beat": 24
}
```

### Fields

| Field             | Type      | Description                                        |
| ----------------- | --------- | -------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| `bpm`             | `float64` | Current tempo in beats per minute.                 |
| `transport_state` | `string`  | One of `stopped`, `start_scheduled`, or `playing`. |
| `start_beat`      | `uint64   | null`                                              | Beat number where playback starts or started. `null` when `transport_state` is `stopped`. |

### Transport states

| State             | `start_beat` | Meaning                                                                                |
| ----------------- | ------------ | -------------------------------------------------------------------------------------- |
| `stopped`         | `null`       | No playback. Consumers should not act on the beat schedule.                            |
| `start_scheduled` | future beat  | Playback will begin at `start_beat`. Consumers prepare and fire together on that beat. |
| `playing`         | past beat    | Playback is running. `start_beat` is the beat playback began at.                       |

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
  "beat.params":   {"bpm": 120.0, "transport_state": "playing", "start_beat": 0}
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
    double                    bpm;
    se_sync_transport_state_t transport_state;
    uint64_t                  start_beat; /* 0 when transport_state is SE_SYNC_STOPPED */
    int                       start_beat_valid; /* 0 when transport_state is SE_SYNC_STOPPED */
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

The internal beat buffer is allocated at consumer creation, sized for N beats.
If N changes it is reallocated on the `SPA_PROP_params` callback path, never
inside `se_sync_get_beats`. `(*out)->params` is always populated regardless of
beat count.

## Cross-graph sync

PipeWire nodes in different clock domains cannot share sample-accurate ticks
directly. A consumer node converts a scheduled beat time stamp to its local
sample position using:

```text
local_sample = current_sample + (beat_nsec - now_nsec) * sample_rate / 1_000_000_000
```

This is as close to sample-accurate as possible without sharing a driver chain.
