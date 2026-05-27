# MIDI Router

The MIDI router manages logical MIDI/control routes for Sonic Eddy. Unlike audio
routing, MIDI/control routing cannot rely on node-level automatic linking:
external hardware is normally exposed through the PipeWire MIDI bridge as a
single node with many ports, so the router must create and remove links between
specific ports.

The router owns these links. The UI edits logical routes, and a router service
materializes them as PipeWire port links. When a route needs filtering or
manipulation, the service inserts a MIDI manipulation node between the source
and target ports instead of creating a direct link.

## Goals

- Route MIDI/control data between specific PipeWire ports.
- Support external hardware exposed through `Midi/Bridge` nodes.
- Persist Sonic Eddy managed MIDI routes.
- Add optional route-level manipulation without changing the external endpoint
  ports.
- Keep unmanaged PipeWire MIDI links visible, but separate from managed routes.

## Non-Goals

- Full graphical patchbay behavior is not required for the first version.
- Arbitrary MIDI scripting is not required initially.
- Audio routing is out of scope.
- Automatic adoption of existing unmanaged links is out of scope unless the user
  explicitly imports or recreates them.

## Route Model

A managed MIDI route connects one source port to one target port.

```typescript
type MidiRoute = {
  id: string;
  name?: string;
  source: MidiPortRef;
  target: MidiPortRef;
  enabled: boolean;
  manipulation: MidiManipulationConfig;
};

type MidiPortRef = {
  objectSerial?: number;
  objectId?: number;
  nodeName?: string;
  portName?: string;
  alias?: string;
  direction: "in" | "out";
};
```

`object.serial` is preferred when available, because it is more stable than
`object.id` during a session. `nodeName + portName` or `alias` can be used as a
fallback for saved routes when serials change across PipeWire restarts.

The router service resolves `source` and `target` against the current port
registry before materializing a route.

## Manipulation Model

Each managed route has a manipulation config. If the config is a passthrough,
the router can create a direct link:

```text
source port -> target port
```

If any manipulation is active, the router creates or reuses a MIDI manipulation
node for that route:

```text
source port -> manipulation.capture
manipulation.playback -> target port
```

The manipulation node is a native C component with a capture stream and a
playback stream. Configuration is exposed as params on the capture node,
matching the looper pattern.

```typescript
type MidiManipulationConfig = {
  dropChannels: number[];
  channelMap: ChannelMapEntry[];
};

type ChannelMapEntry = {
  from: number;
  to: number;
};
```

MIDI channels are `1..16` in persisted config and UI. The native implementation
can convert to `0..15` internally.

Initial operations:

- `dropChannels`: drop all channel voice messages for the listed channels.
- `channelMap`: rewrite channel voice messages from one channel to another.

Channel voice messages include note off, note on, poly pressure, control change,
program change, channel pressure, and pitch bend. System messages are passed
through unchanged in the first version.

If a channel is both dropped and mapped, drop wins.

## Native MIDI Manipulation Node

The native node should be created by a C lifecycle API, similar to the looper.

```c
typedef struct se_midi_manipulator_config {
  const char *name;
  const char *description;
  const char *tag;
} se_midi_manipulator_config;

size_t se_midi_manipulator_create(const se_midi_manipulator_config *config);
void se_midi_manipulator_destroy(size_t handle);
```

The C# API should expose a managed object with the capture node, playback node,
and handle for destruction.

```typescript
type MidiManipulator = {
  name: string;
  tag: string;
  handle: number;
  captureNodeObjectSerial: number;
  playbackNodeObjectSerial: number;
};
```

The node shape is fixed:

- capture node: `Stream/Input/Midi`, used as the route input and parameter
  endpoint.
- playback node: `Stream/Output/Midi`, used as the route output.

The PipeWire stream setup should match the existing native MIDI controller
nodes:

- `media.type=Midi`
- `media.category=Capture` on the capture stream
- `media.category=Playback` on the playback stream
- `media.role=DSP`
- `media.class=Stream/Input/Midi` on the capture stream
- `media.class=Stream/Output/Midi` on the playback stream
- `format.dsp=8 bit raw midi` for the capture stream initially

The existing controller receiver accepts both `SPA_CONTROL_Midi` and
`SPA_CONTROL_UMP`, while the existing sender emits UMP. The first manipulator
implementation should prefer preserving the incoming event encoding where
possible. If that is awkward, it can accept the same input as the existing
receiver and emit UMP like the existing sender.

The node must not rely on automatic node-level linking for endpoint routing. The
router creates explicit links between ports.

### Params

Config is passed to the capture node.

```typescript
type MidiManipulatorParams = {
  "midi.router.config": MidiManipulatorConfigJson;
};

type MidiManipulatorConfigJson = {
  version: 1;
  drop_channels: number[];
  channel_map: Array<[number, number]>;
};
```

Example:

```json
{
  "version": 1,
  "drop_channels": [10],
  "channel_map": [
    [1, 2],
    [3, 4]
  ]
}
```

The native process function applies the current config to every event in the
buffer. Param updates are read on the PipeWire side and made visible to the
process function via an audio-thread-safe snapshot, queue, or atomic shared
state.

## Router Service

The router service owns managed routes and their materialized PipeWire state.

Responsibilities:

- Track configured routes.
- Resolve route endpoint ports.
- Create direct links for passthrough routes.
- Create MIDI manipulation nodes for manipulated routes.
- Link source -> manipulator -> target.
- Update manipulation node params when a route config changes.
- Delete stale links when routes are changed, disabled, removed, or endpoints
  disappear.
- Destroy unused manipulation nodes.
- Expose routes to the UI.
- Persist routes.

The service should subscribe to port and link registry changes. When the
PipeWire graph changes, it reconciles desired routes against actual links.
Registry changes are also the retry mechanism for unresolved routes. If a saved
hardware port is missing at startup, the route remains unresolved; when the port
appears later, reconciliation creates the required link or manipulation chain.

## Materialization

For each enabled route:

1. Resolve the source and target ports.
2. If either port is missing, mark the route unresolved and do not create links.
3. If manipulation is passthrough, ensure a direct link exists.
4. If manipulation is active:
   - ensure a manipulator node exists for the route,
   - set its config params,
   - resolve its input/output MIDI ports,
   - ensure `source -> manipulator input` exists,
   - ensure `manipulator output -> target` exists.
5. Remove any stale links previously owned by the route.

The first version uses one manipulator node per managed route. Manipulator nodes
are not shared even when two routes have identical manipulation configs. This
keeps lifecycle, ownership, debugging, and later route-specific controls simple.

The first version can track owned links in memory by route id and port ids. The
persisted state stores logical routes, not PipeWire link ids.

## Persistence

Routes should be stored in Sonic Eddy app data using the same persisted model
pattern used elsewhere. The storage format can be generated from protobuf/gRPC
models, but the logical data should match:

```typescript
type MidiRouterState = {
  version: 1;
  routes: MidiRoute[];
};
```

PipeWire link ids and node object ids are runtime details and should not be
stored as authoritative route identity.

## UI

The first UI should stay explicit and robust:

- source port selector,
- target port selector,
- connect button,
- route table,
- delete route button,
- enabled toggle,
- manipulation controls per route.

Initial manipulation controls:

- channel drop selector for channels `1..16`,
- channel map rows: `from` channel, `to` channel, delete row,
- add channel map row.

The UI should also show unmanaged MIDI links separately or mark them as
unmanaged. The router does not import or manage links it did not create. Managed
route deletion goes through the router service; unmanaged links are left alone,
matching WirePlumber-style ownership behavior.

## Decisions

- Use the same MIDI stream shape as the existing native MIDI controller nodes.
- Use one manipulation node per managed route. Do not share manipulation nodes.
- Retry unresolved routes on registry changes.
- Do not import unmanaged links. If Sonic Eddy did not create a link, the MIDI
  router does not own or mutate it.
