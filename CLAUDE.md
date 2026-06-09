# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build the solution (also copies the native libfrsonic .so from fr-sonic/build/src/)
dotnet build SonicEddy.sln

# Run the app
dotnet run --project SonicEddy/

# Run all tests
dotnet test SonicEddy.Tests/

# Run a specific test
dotnet test SonicEddy.Tests/ --filter "FullyQualifiedName~TestClassName"

# Build the native fr-sonic library (run before dotnet build when C++ changes)
ninja -C fr-sonic/build
```

## Architecture Overview

Sonic Eddy is a performance-focused audio mixer for Linux, built on top of PipeWire/WirePlumber. The UI is **Avalonia** with **ReactiveUI** (MVVM). Dependency injection uses **Splat** with manual registration in `App.axaml.cs`.

### Projects

| Project | Purpose |
|---|---|
| `SonicEddy/` | Main Avalonia app — views, view models, services |
| `SonicEddy.Contracts/` | Shared record types for FilterGraph, Mixer, Parameters; serialized with protobuf-net |
| `SonicEddy.Tests/` | xUnit tests with NSubstitute |
| `Fr.Pw.Midi/` | C# P/Invoke wrapper around the native MIDI library |
| `Fr.Pw.Midi.Console/` | Console harness for testing MIDI |
| `pw-midi-mapper/` | Native C++ library (Meson build) that reads PipeWire MIDI events and dispatches callbacks |

### Audio routing via PipeWire loopback modules

Every mixer channel is a chain of **PipeWire loopback modules** created at runtime:

```
External audio source
    → InputLoopback (capture+playback nodes)
    → [optional FilterChain (capture+playback nodes with LV2 plugins)]
    → OutputLoopback (capture+playback nodes)
    → MasterChannel InputLoopback
    → MasterChannel OutputLoopback
    → Physical output
```

Send effects follow the same pattern: each channel has N `SendLoopback` modules that feed into `ReturnChannel`s which merge back into the master.

`MixerEditor` (`Services/MixerServiceV2/MixerEditor.cs`) constructs these loopback chains by calling `IWireplumberService.CreateLoopbackModule`. `MixerService` owns the `Mixer` state, serializes concurrent access with two `SemaphoreSlim`s (`_externalChange` / `_internalChange`), and queues PipeWire `NodeAdded` events that arrive during mixer construction in `_pendingAddedNodes`.

A `Mixer` contains `List<MixerLayer>`. By default two layers are created — the second layer receives the same `InputChannel`/`OutputChannel` lists as the first.

### FilterGraph

A `FilterGraph` (`SonicEddy.Contracts/FilterGraph/`) is a directed graph of `FilterGraphNodeBase` subtypes (Input, Lv2Plugin, Output) connected by `FilterGraphEdge`s. Graphs are serialized to protobuf binary files with extension `.fc` under `~/.local/share/SonicEddy/FilterGraph/`.

When applied to a channel strip, `MixerEditor.AddFilterToChannelStrip` converts the `FilterGraph` to a `FilterChainModuleConfig` and creates a PipeWire `FilterChain` module that inserts between the channel's loopback pair.

### MIDI

`pw-midi-mapper/` is a C++ Meson library (`libfrmidimapper.so.0.0.4`) that opens PipeWire MIDI ports and calls registered C callbacks on MIDI events. `Fr.Pw.Midi/PInvoke/FrPwMidiLib.cs` P/Invokes into this library. `Fr.Pw.Midi/FrPwMidi.cs` is the static C# facade that starts the MIDI processor and raises typed events. `MidiControllerService` (`Services/Midi/`) subscribes to those events and re-raises them for the view model layer.

### Data persistence

`AppDataService` stores data under `~/.local/share/SonicEddy/`:
- `FilterGraph/*.fc` — protobuf-encoded `FilterGraph` records
- `Mixer/*.mix` — protobuf-encoded `Mixer` records
- `Preferences/preferences.grpc` — protobuf-encoded `Preferences`

### Key conventions

- `ImplicitUsings` is **disabled** in `SonicEddy.csproj` — all `using` statements must be explicit.
- `AvaloniaUseCompiledBindingsByDefault` is enabled — bindings are compiled and type-checked.
- Channel and node IDs are `ulong` throughout; `ObjectSerial` is the stable per-session WirePlumber serial number.
- All mixer mutation goes through `IMixerService.GetAndLock()` / `Unlock()` to avoid races with WirePlumber node events.

### System requirement

PipeWire's default file descriptor limit is too low for the many loopback modules created by a full mixer. Create `~/.config/systemd/user/pipewire.service.d/limits.conf` with:

```ini
[Service]
LimitNOFILE=65536
```

Then restart PipeWire.
