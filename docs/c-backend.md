# C Backend Architecture

## Current State

There are four separate native libraries, each independently initialised and
running its own event loop and PipeWire connection.

### `fr-wireplumber` → `libfrwireplumber.so`

The most complex backend. `WireplumberThread` owns a GLib `GMainLoop` +
`GMainContext` and a `WpCore`. WirePlumber internally creates and manages a
`pw_core`, `pw_context`, and `pw_loop` that are accessible via the core. The
loop runs on a dedicated thread started by `start()`. All PipeWire and
WirePlumber work happens on that thread.

Exported C functions (all unnamespaced):

```
init(…callbacks…)   — create WireplumberThread, register all callbacks
start()             — launch the GLib main loop on a new thread
stop()              — quit the loop and join the thread

set_volumes / set_mute / set_params   — property mutation
load_module / destroy_module          — PipeWire module lifecycle
set_metadata_entry / delete_metadata_entry
create_link_by_port_ids / delete_link_by_object_id / delete_link
```

Callbacks fired from the GLib loop thread:

- `node_added`, `object_deleted`
- `props_changed`, `props_enum_failed`, `prop_info_added`
- `metadata_added`, `metadata_entry_updated`, `metadata_entry_deleted`

### `fr-monitoring` → `libfrmonitoring.so`

Runs its **own** `pw_main_loop` on a dedicated thread, separate from and
unrelated to the WirePlumber loop. Also runs a second timer thread that
forwards peak measurements at a configured interval.

Monitoring streams are PipeWire capture streams. They are created on the caller
thread and posted to the PipeWire loop via `pw_loop_invoke`.

Exported C functions:

```
init(peak_callback, update_interval_ms)
start() / stop()
start_monitor_node(object_serial)
stop_monitor_node(object_serial)
```

### `fr-lv2` → `libfrlv2.so`

Pure LV2 plugin discovery. No PipeWire, no loop, no threads.

```
init() / destroy()
plugin_descriptions_json() → const char *
plugin_classes_json()      → const char *
```

### `pw-midi-mapper` → `libfrmidimapper.so`

Runs its **own** `pw_main_loop` (not GLib-based) on a dedicated thread. No
WirePlumber. Handles MIDI controller input and maps it to mixer state.

```
init(midi_cc_callback)
start() / stop()
create_midi_mix_port / create_mm_1_port / create_fader_fox_pc4_port
set_selected_channel / clear_selected_channel / set_selected_layer
set_channel_node / set_master_channel_node
set_channel_filter_node / set_channel_send_node
clear_filter_parameters / add_filter_parameter
set_selected_plugin_page
```

### Problems

- Three separate PipeWire connections with three `pw_init` calls.
- All four libraries export bare `init`, `start`, `stop` — unusable from a
  single address space without symbol renaming.
- Four separate build projects and four `.so` files to ship.
- No shared infrastructure: each library rediscovers nodes, loops, etc.
- Adding new functionality (e.g. the sync master) requires yet another library
  or awkwardly grafting onto one of the existing ones.

---

## Target Architecture

### Overview

One C/C++ library — `libfrsonic.so` — with a single PipeWire + WirePlumber
event loop shared across all subsystems. One C# classlib wrapping it. One
console test app.

```
libfrsonic/
  include/
    frsonic.h              — unified public C API (all symbols prefixed frsonic_)
  src/
    frsonic.cpp            — entry point; single global Core instance
    core/
      Core.h / Core.cpp    — owns GMainLoop, WpCore, pw_loop; replaces WireplumberThread
    wireplumber/           — object manager, props, metadata, modules, links
    monitoring/            — peak-level streams
    midi/                  — MIDI controller mapping
    lv2/                   — LV2 plugin discovery
    sync/                  — sync master and consumer (see sync.md)
```

### Event Loop

WirePlumber requires a GLib `GMainLoop`. The GLib loop also provides the
`pw_loop` that all PipeWire work runs on. `Core` owns these:

```
GMainContext → GMainLoop   (runs on one dedicated thread)
WpCore       → pw_core, pw_context, pw_loop
```

All PipeWire interactions from every subsystem use this single `pw_loop`.
`Core` passes the `pw_loop` reference (obtained from `WpCore`) to each subsystem
at initialisation. Operations that originate off the loop thread are posted via
`pw_loop_invoke`.

The monitoring subsystem's streams already use `pw_loop_invoke` for setup; they
are moved onto the shared loop instead of creating their own. The peak timer
thread is retained as-is and does not need a loop.

The MIDI subsystem receives the shared `pw_loop` from `Core` and uses it
directly, replacing the `pw_main_loop` it currently owns. No indirection is
needed — MIDI port creation and node interactions run on the same loop thread as
everything else.

The sync master (see `sync.md`) runs entirely on the shared loop — its timer
and `SPA_PROP_params` updates are loop-thread operations.

### C API

All exported symbols are prefixed `frsonic_`. The library has one lifecycle:

```c
/* Initialise all subsystems. Must be called before start(). */
void frsonic_init(frsonic_callbacks_t *callbacks);

/* Start the GLib/PipeWire loop on a background thread. */
void frsonic_start();

/* Stop the loop and join the thread. */
void frsonic_stop();
```

Subsystem functions keep their logical grouping under the `frsonic_` prefix:

```c
/* WirePlumber / object model */
void frsonic_set_volumes(uint64_t object_id, const double *volumes, size_t count);
void frsonic_set_mute(uint64_t object_id, bool mute);
void frsonic_set_params(uint64_t object_id, ...);
bool frsonic_load_module(const char *name, const char *config, void **handle);
void frsonic_destroy_module(void *handle);
void frsonic_set_metadata_entry(...);
void frsonic_delete_metadata_entry(...);
WpLink *frsonic_create_link_by_port_ids(
  uint64_t out_port, uint64_t in_port, bool linger);
void frsonic_delete_link_by_object_id(uint64_t object_id);
void frsonic_delete_link(WpLink *link);

/* Monitoring */
void frsonic_start_monitor_node(uint64_t object_serial);
void frsonic_stop_monitor_node(uint64_t object_serial);

/* MIDI */
size_t frsonic_create_midi_mix_port(...);
size_t frsonic_create_mm1_port(...);
size_t frsonic_create_fader_fox_pc4_port(...);
void frsonic_set_selected_channel(...);
/* … remaining MIDI mapper functions … */

/* LV2 */
const char *frsonic_lv2_plugin_descriptions_json();
const char *frsonic_lv2_plugin_classes_json();

/* Sync consumer (see sync.md for full API) */
se_sync_consumer_t *frsonic_sync_consumer_create();
void frsonic_sync_consumer_destroy(se_sync_consumer_t *consumer);
int  frsonic_sync_get_beats(se_sync_consumer_t *consumer, uint64_t now_nsec,
                            uint32_t sample_rate, se_sync_result_t **out);
void frsonic_sync_request_bpm(...);
void frsonic_sync_request_transport_state(...);
```

The `frsonic_callbacks_t` struct collects all callbacks that were previously
passed to individual `init` functions:

```c
typedef struct {
    /* WirePlumber object model */
    node_added_callback_pointer_t            node_added;
    props_changed_callback_pointer_t         props_changed;
    props_enum_failed_callback_pointer_t     props_enum_failed;
    prop_info_added_callback_pointer_t       prop_info_added;
    object_deleted_callback_pointer_t        object_deleted;
    metadata_added_callback_pointer_t        metadata_added;
    metadata_entry_updated_callback_pointer_t metadata_entry_updated;
    metadata_entry_deleted_callback_pointer_t metadata_entry_deleted;

    /* Monitoring */
    peak_callback_pointer_t                  peak;
    uint64_t                                 peak_update_interval_ms;

    /* MIDI */
    MidiControlChangeUpdateCallbackPtr       midi_cc_update;
} frsonic_callbacks_t;
```

### C# Side

`Fr.Sonic` is a single classlib that replaces `Fr.Wireplumber`,
`Fr.Pw.Monitoring`, and `Fr.Pw.Midi`. It P/Invokes into `libfrsonic.so` only.

```
Fr.Sonic/
  PInvoke/
    FrSonicLib.cs          — all DllImport declarations
  FrSonic.cs               — static facade: Init, Start, Stop
  FrSonicWireplumber.cs    — object model events and mutation methods
  FrSonicMonitoring.cs     — PeakChanged event, StartMonitorNode, StopMonitorNode
  FrSonicMidi.cs           — MIDI events and controller state
  FrSonicLv2.cs            — plugin discovery
  FrSonicSync.cs           — sync consumer (see sync.md C# API)
  runtimes/
    linux-x64/
      libfrsonic.so.X.Y.Z
```

`FrSonic.Init(...)` wires up all `[UnmanagedCallersOnly]` internal callbacks and
calls `frsonic_init`. `FrSonic.Start()` / `FrSonic.Stop()` map directly to the
C functions.

### Build

One Meson project at `fr-sonic/` replaces the four separate projects.

```
fr-sonic/
  meson.build       — project('fr-sonic', 'cpp', ...)
  include/
    frsonic.h
  src/
    meson.build     — one shared_library target + one executable target
    frsonic.cpp
    core/
    wireplumber/
    monitoring/
    midi/
    lv2/
    sync/
    apps/
      test_app.cpp  — console harness
```

Dependencies: `libpipewire-0.3`, `wireplumber-0.5`, `boost` (for MIDI mapper),
LV2 (for plugin discovery). No new external dependencies.

One fish build script (`update_fr_sonic.fish`) replaces the individual scripts:

```fish
cd fr-sonic
rm -rf release
meson setup release --buildtype release --optimization 3
cd release
meson compile
mkdir -p ../../Fr.Sonic/runtimes/linux-x64
cp ./src/libfrsonic.so.X.Y.Z ../../Fr.Sonic/runtimes/linux-x64/
```

### Migration Steps

1. Create `fr-sonic/` with the `Core` class combining `WireplumberThread` and
   the monitoring loop into a single GLib/pw loop.
2. Port each subsystem directory from its current library, renaming all exported
   symbols with the `frsonic_` prefix.
3. Pass `Core`'s shared `pw_loop` (from `WpCore`) into each subsystem at init;
   remove per-subsystem loop ownership.
4. Replace the four separate Meson builds with the single `fr-sonic/meson.build`.
5. Create `Fr.Sonic` classlib; migrate P/Invoke declarations from
   `Fr.Wireplumber`, `Fr.Pw.Monitoring`, `Fr.Pw.Midi` into `FrSonicLib.cs`.
6. Update `SonicEddy.csproj` to reference `Fr.Sonic` instead of the three
   individual libraries.
7. Remove the four old library directories and their corresponding C# wrappers.
