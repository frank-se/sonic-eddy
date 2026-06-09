# Ableton Link Integration

Ableton Link lets Sonic Eddy act as the authoritative beat clock for a Link
session, so connected apps (MPC, Ableton Live, etc.) follow Sonic Eddy's tempo
and transport over the local network.

Link has no formal master/slave distinction — all peers are equal in the protocol
— but continuously committing Sonic Eddy's current beat position and tempo on
every timer fire means peers always converge to our timeline. Incoming tempo
changes from peers are ignored.

## Goals

- Push Sonic Eddy's tempo and beat position to the Link session continuously
- Distribute transport start/stop to all Link peers (quantum-aligned)
- Ignore incoming tempo/transport changes from other peers
- When Link is disabled, fall back to the internal sync master as today
- Expose peer count and a quantum setting to the UI

---

## Architecture

Link does not replace `SyncMaster`. A new `LinkSyncAdapter` class sits alongside
it and reads from it. `SyncMaster` remains the sole owner of the beat schedule
and the authoritative source of tempo and transport. The adapter's only job is
to keep the Link session state in sync with `SyncMaster`.

```
SyncMaster  ──── read current beat/tempo/transport ──▶  LinkSyncAdapter
     ▲                                                         │
     │                                                         ▼
UI / C# layer                                          Link network
                                                             │
                                                    Other Link peers (MPC, Live …)
```

`LinkSyncAdapter` lives on the PipeWire main loop thread. On each timer fire it
reads the current sync master state and commits it to Link's app-thread session
state. No Link callbacks are registered for tempo or transport — incoming changes
from peers are simply never applied.

---

## Clock Correlation

Link's clock: `link.clock().micros()` — microseconds, arbitrary epoch.  
Sync master clock: `CLOCK_MONOTONIC` — nanoseconds since boot.

At initialisation, and re-sampled every 10 seconds to absorb drift:

```cpp
const auto link_us   = link.clock().micros().count();
const auto mono_ns   = now_nsec();  // clock_gettime(CLOCK_MONOTONIC)
const auto offset_ns = (int64_t)mono_ns - link_us * 1000;
```

Conversion: `mono_ns(link_us) = link_us * 1000 + offset_ns`  
Inverse: `link_us(mono_ns) = (mono_ns - offset_ns) / 1000`

The adapter always uses the most recently sampled offset.

---

## Pushing State to Link

On each timer fire the adapter:

1. Re-correlates clocks if due.
2. Reads the current sync master state:
   - Current beat: `master.current_beat(now_nsec())`
   - Current tempo: `master.current_bpm()`
   - Transport state: playing / stopped
3. Captures app-thread session state: `link.captureAppThreadSessionState()`
4. Commits tempo and beat position:

```cpp
const auto link_time_now = ableton::Link::Clock::Micros{
    (now_nsec() - _clock_offset_ns) / 1000
};

state.setTempo(bpm, link_time_now);
state.requestBeatAtTime(
    static_cast<double>(sync_beat),  // integer beat, fractional part = 0
    link_time_now,
    1.0   // quantum=1 for per-beat alignment; use _quantum for bar alignment
);
link.commitAppThreadSessionState(state);
```

Committing on every timer fire (250 ms) continuously reasserts our position.
Peers that nudge tempo between our commits will snap back within one cycle.

---

## Transport

Transport start/stop is committed to Link alongside beat and tempo. When Sonic
Eddy starts playback, set `state.setIsPlaying(true, link_time_now)` before
committing. When stopped, `state.setIsPlaying(false, link_time_now)`.

Link distributes the start/stop to peers. Peers with start/stop sync enabled will
follow at the next quantum boundary per their own quantum setting.

The adapter mirrors the sync master's transport state on every timer fire, so no
separate path is needed for transport changes — they are picked up automatically
on the next commit.

---

## Quantum

The quantum controls phase alignment as seen by Link peers. It does not affect the
sync master's internal beat numbering.

Default: **4 beats** (one bar of 4/4).

For the `requestBeatAtTime` call the adapter uses **quantum = 1.0** so that every
integer beat from the sync master maps cleanly to a Link beat boundary. The
`_quantum` field is exposed to the UI but is only passed to Link's
`beatAtTime`/`phaseAtTime` queries if those are ever needed for UI display (e.g.,
a bar-phase indicator). It is not used in the commit path.

---

## Peer Count

`link.numPeers()` counts other participants (not counting Sonic Eddy itself).
Register `link.setNumPeersCallback` to be notified when peers join or leave. The
callback fires on a Link background thread; post to the PipeWire main loop before
touching any state.

---

## C++ API (`LinkSyncAdapter`)

```cpp
namespace sesync {

class LinkSyncAdapter {
public:
  LinkSyncAdapter(pw_loop *loop, SyncMaster *master);
  ~LinkSyncAdapter();

  void enable(bool enable);
  [[nodiscard]] bool is_enabled() const;
  [[nodiscard]] std::size_t num_peers() const;

  void set_quantum(double quantum);  // default 4.0; UI display only
  void set_peers_callback(std::function<void(std::size_t)> cb);

private:
  void on_timer();
  void push_state();
  void recorrelate_clocks();

  pw_loop        *_loop;
  SyncMaster     *_master;
  pw_timer_queue *_timer_queue = nullptr;
  pw_timer        _timer{};

  ableton::Link _link{120.0};
  double   _quantum         = 4.0;
  int64_t  _clock_offset_ns = 0;
  int64_t  _next_recorrelate = 0;  // monotonic nsec

  std::function<void(std::size_t)> _peers_callback;

  static void timer_callback(void *data);
};

} // namespace sesync
```

The timer fires every **250 ms**.

No `_tempo_dirty` or `_transport_dirty` flags — the adapter unconditionally
commits the current sync master state on every fire.

---

## C# API

```csharp
public static class FrSonicLink
{
    public static void Enable(bool enable);
    public static bool IsEnabled { get; }
    public static int NumPeers { get; }
    public static double Quantum { get; set; }

    public static event EventHandler<int> NumPeersChanged;
}
```

`NumPeersChanged` is raised on the PipeWire main loop thread; marshal to the UI
thread before touching UI state.

The `SynchronizationViewModel` does not need to change its BPM/transport control
path — those continue to go to `FrPwSync` (the sync master) as today. Link
follows automatically.

---

## UI

Add to the Synchronization window:

```
┌─ Ableton Link ──────────────────────────────────┐
│  [x] Enable Link                                │
│  Peers connected: 2                             │
│  Quantum: [4 beats ▼]                           │
└─────────────────────────────────────────────────┘
```

---

## Build

Link is header-only. Add as a git submodule:

```
fr-sonic/third_party/link/
```

In `meson.build`:

```meson
link_inc = include_directories(
  'third_party/link/include',
  'third_party/link/modules/asio-standalone/asio/include'
)

add_project_arguments('-DLINK_PLATFORM_LINUX=1', language: 'cpp')
```

Link requires threading support (`-pthread`), provided by `dependency('threads')`
which Meson adds by default on Linux.

---

## Open Questions

1. **`start_scheduled` follow-up**: The sync master's `transport_state` entry
   for `start_scheduled` must eventually transition to `playing`. Check whether
   `SyncMaster::activate_due_changes` handles this automatically or whether the
   adapter needs to send a second commit after the start beat arrives.

2. **Beat phase on peer join**: When a new peer joins mid-session it receives the
   current Link beat position. Since the adapter commits integer beats with
   fractional part = 0, the peer's phase alignment depends on how far through the
   current beat we are at commit time. Consider whether sub-beat accuracy matters
   for the MPC use case or if ±1 beat drift on join is acceptable.
