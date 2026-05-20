# Monitoring

## What the current implementation does (and why it's wrong)

`Stream::process()` is called by PipeWire on the RT thread once per buffer
(typically 128–512 samples at 48 kHz ≈ 2.7–10.7 ms of audio). It computes
`max(|sample|)` and `mean(|sample|)` over that single buffer and stores them
into four `std::atomic<float>` fields.

`Monitor::forward_measures()` runs on a separate thread every
`update_interval` ms (e.g. 250 ms). By the time it reads those atomics,
`process()` has fired ~20–90 times and overwritten them each time. The UI
therefore sees the peak and average of only the **most recent buffer** —
roughly 5–10 ms of audio — which is effectively random noise at the meter
update rate.

Two separate problems:

1. **No time window.** Peak and average should span the full interval between
   UI updates (or a configurable window), not just the last buffer.

2. **Wrong average metric.** `mean(|x|)` (mean absolute value) is not a
   standard audio metric. RMS (`sqrt(mean(x²))`) is correct because it is
   proportional to signal power and correlates with perceived loudness.

---

## Correct design

### Per-buffer entry

Each call to `process()` produces one entry:

```
struct BufferEntry {
    steady_clock::time_point timestamp;
    float peak[2];       // max(|sample|) per channel in this buffer
    float sum_sq[2];     // sum(sample²) per channel
    uint32_t samples;    // sample count per channel
};
```

### Sliding window

`Stream` holds a fixed-size ring buffer of `BufferEntry` values (e.g. capacity
= 512, enough for ~5 s at 128-sample buffers and 48 kHz).

When the update thread reads metrics it:

1. Snapshots the current `head` and `tail` indices.
2. Discards entries whose `timestamp < now - window_duration`.
3. Over the remaining entries computes:
   - **Peak** = `max(entry.peak[c])` across all entries, per channel.
   - **RMS** = `sqrt(total_sum_sq[c] / total_samples)` across all entries,
     per channel.

`window_duration` should default to the `update_interval` so the meter
reflects exactly the audio that arrived since the last UI update. Making it
configurable (e.g. 50–500 ms) allows trading smoothness for responsiveness.

### Peak hold

A peak hold prevents the meter needle from dropping immediately when signal
level falls, which makes transients readable at normal meter update rates.

The update thread maintains per-channel:

```
float   held_peak          = 0;
time_point held_peak_time  = {};  // when held_peak was last raised
```

After computing the window peak `w`:

- If `w >= held_peak`: update `held_peak = w` and `held_peak_time = now`.
- Else if `now - held_peak_time < hold_duration`: report `held_peak`
  (hold phase).
- Else: `held_peak` decays toward `w` (or snaps immediately — decay is
  optional but looks better).

`hold_duration` should default to 1000–2000 ms and be configurable.

The callback receives both the live sliding-window value and the held peak so
the UI can draw them independently (e.g. a bar for RMS, a tick for held peak).

### Concurrency

`process()` runs on the PipeWire RT thread. Blocking (mutex lock) is not
allowed there. The ring buffer must be lock-free.

Use a single-producer / single-consumer (SPSC) ring buffer:

- **Producer** (`process()`, RT thread): writes `entries[tail & mask]` then
  increments `tail` with `std::atomic::store(release)`.
- **Consumer** (update thread): reads `tail` with `load(acquire)`, iterates
  from its local `head` up to `tail`, advances `head` past expired entries.

No locks. No allocations on the RT thread. The ring buffer is pre-allocated in
the `Stream` constructor.

### Update thread

Replace the current `while(true) / sleep / SIGINT` pattern with:

```
std::atomic<bool> _running = false;
std::mutex        _cv_mutex;
std::condition_variable _cv;

// stop():
_running = false;
_cv.notify_one();
_update_thread->join();

// loop body:
while (_running) {
    forward_measures();
    std::unique_lock lk(_cv_mutex);
    _cv.wait_for(lk, _update_interval, [this]{ return !_running; });
}
```

### Callback signature

Extend to carry both live RMS and held peak per channel:

```cpp
void(uint64_t object_serial,
     float left_rms,   float right_rms,
     float left_peak,  float right_peak)   // held-peak values
```

The live window peak is an intermediate value the update thread uses
internally; only the held peak is reported to callers.

---

## Parameters

| Parameter         | Default  | Description                                      |
|-------------------|----------|--------------------------------------------------|
| `window_duration` | equal to `update_interval` | Time span of the sliding window |
| `hold_duration`   | 1500 ms  | How long the held peak stays before decaying     |
| `update_interval` | 100 ms   | How often the callback fires (currently 250 ms — 100 ms is more responsive for a live meter) |
| ring buffer size  | 512 entries | Must exceed `window_duration / min_buffer_period` |
