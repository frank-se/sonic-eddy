# Filter Graph — Built-in Nodes

PipeWire's `filter-chain` module ships a set of built-in signal processing nodes
alongside LV2 plugin support. These are lighter weight, have no external
dependencies, and cover common graph-wiring needs (mixing, copying, math
transforms, gates). This document specifies how to add them to the filter graph
editor.

Reference: https://docs.pipewire.org/page_module_filter_chain.html

---

## Built-in node catalog

Each row defines how the node appears in the UI and how it serialises. The
`pw_name` is the value written to `FilterGraphNode.Name` in the PipeWire JSON
config. Port names are the exact strings PipeWire expects on link endpoints.

Control parameters are initial values written to `FilterGraphNode.Control`. In
this first version they are configured per-node in the graph rather than being
wired as live control signals (control port routing is a future concern).

| Display name | `pw_name` | Audio in ports | Audio out ports | Control params (name → default) |
|---|---|---|---|---|
| Mixer | `mixer` | In 1 … In N (2–8, user-selectable) | Out | Gain 1 … Gain N → 1.0 |
| Copy | `copy` | In | Out | — |
| Invert | `invert` | In | Out | — |
| Linear | `linear` | In | Out | Mult → 1.0, Add → 0.0 |
| Clamp | `clamp` | In | Out | Min → −1.0, Max → 1.0 |
| Reciprocal | `recip` | In | Out | — |
| Abs | `abs` | In | Out | — |
| Sqrt | `sqrt` | In | Out | — |
| Exp | `exp` | In | Out | Base → 2.718281828 |
| Log | `log` | In | Out | Base → 10.0, M1 → 1.0, M2 → 1.0 |
| Multiply | `mult` | In 1 … In N (2–8) | Out | — |
| Sine | `sine` | — | Out | Freq → 440.0, Ampl → 1.0, Offset → 0.0, Phase → 0.0 |
| Max | `max` | In 1 … In N (2–8) | Out | — |
| DC Block | `dc_block` | In 1 … In N (1–8) | Out 1 … Out N | R → 0.995 |
| Ramp | `ramp` | — | Out | Start → 0.0, Stop → 1.0, Duration (s) → 1.0 |
| Debug | `debug` | In | Out | — |
| Zero Ramp | `zeroramp` | In | Out | Gap (s) → 0.000666, Duration (s) → 0.000666 |
| Noise Gate | `noisegate` | In | Out | Close threshold → 0.01, Open threshold → 0.02, Hold (s) → 0.1, Attack (s) → 0.01, Release (s) → 0.1 |

**Variable-input nodes** (Mixer, Multiply, Max, DC Block): the user configures
the channel count at node creation time (via a small dialog or a number field in
the toolbox). The count is stored on the contract and determines which numbered
ports are emitted in the config. Unused numbered ports are simply omitted.

---

## Contracts layer (`SonicEddy.Contracts`)

### New record: `FilterGraphBuiltinNode`

Add alongside `FilterGraphLv2Plugin`:

```csharp
[ProtoContract]
public record FilterGraphBuiltinNode(
    Guid Id,
    string Name,
    [property: ProtoMember(1)] BuiltinNodeType NodeType,
    [property: ProtoMember(2)] int ChannelCount,           // meaningful for multi-input nodes
    [property: ProtoMember(3)] List<FilterGraphBuiltinPort> InputPorts,
    [property: ProtoMember(4)] List<FilterGraphBuiltinPort> OutputPorts,
    [property: ProtoMember(5)] List<FilterGraphBuiltinControl> InitialControls)
    : FilterGraphNodeBase(Id, Name)
{
    public FilterGraphBuiltinNode()
        : this(Guid.Empty, string.Empty, BuiltinNodeType.Copy,
               1, [], [], []) { }
}
```

### New enum: `BuiltinNodeType`

```csharp
public enum BuiltinNodeType
{
    Mixer, Copy, Invert, Linear, Clamp, Reciprocal, Abs, Sqrt, Exp, Log,
    Multiply, Sine, Max, DcBlock, Ramp, Debug, ZeroRamp, NoiseGate
}
```

### Port type for built-ins

Reuse the existing port record structure but with a dedicated type to keep
protobuf IDs independent of LV2 changes:

```csharp
[ProtoContract]
public record FilterGraphBuiltinPort(
    [property: ProtoMember(1)] Guid Id,
    [property: ProtoMember(2)] string Name)  // "In", "Out", "In 1", etc.
{
    public FilterGraphBuiltinPort() : this(Guid.Empty, string.Empty) { }
}
```

### Control value record

```csharp
[ProtoContract]
public record FilterGraphBuiltinControl(
    [property: ProtoMember(1)] string Name,   // "Mult", "Add", "Gain 1", …
    [property: ProtoMember(2)] double Value)
{
    public FilterGraphBuiltinControl() : this(string.Empty, 0) { }
}
```

### `FilterGraphNodeBase` — add ProtoInclude

```csharp
[ProtoInclude(103, typeof(FilterGraphBuiltinNode))]
```

The number 103 follows the existing 100–102.

---

## Config conversion (`Fr.Sonic.Model.Config.FilterChain`)

`FilterGraphBuiltinNode` maps to a `FilterGraphNode` as follows:

```csharp
new FilterGraphNode
{
    Name  = nodeViewModel.Name,           // unique name in the graph, e.g. "mixer1"
    Type  = "builtin",
    Plugin = builtinNode.NodeType.ToPwName(),
    Control = builtinNode.InitialControls
                  .ToDictionary(c => c.Name, c => (object)c.Value)
}
```

`ToPwName()` is a small extension method on `BuiltinNodeType` that returns the
`pw_name` string from the catalog table above.

Links to/from builtin nodes follow the same `"nodename:portname"` format already
used for LV2 links. Port names are the exact strings from the catalog
(e.g. `"mixer1:In 1"`, `"mixer1:Out"`).

The `FilterGraphConfig.Inputs` and `FilterGraphConfig.Outputs` lists may
reference builtin node port names just like LV2 ports — this is already
supported by the current `FilterGraphConfig` shape.

---

## UI layer (`SonicEddy`)

### Built-in node catalog helper

A static class `BuiltinNodeCatalog` in `ViewModels/FilterGraphBuilderViewModels`
returns the canonical definition for each `BuiltinNodeType`: display name, port
list, and default control values. This is the single source of truth used by
both the toolbox and by `BuiltinNodeViewModel`.

```csharp
public static class BuiltinNodeCatalog
{
    public static BuiltinNodeDefinition Get(BuiltinNodeType type);
}

public record BuiltinNodeDefinition(
    BuiltinNodeType Type,
    string DisplayName,
    IReadOnlyList<BuiltinPortDef> AudioInPorts,
    IReadOnlyList<BuiltinPortDef> AudioOutPorts,
    IReadOnlyList<BuiltinControlDef> Controls);

public record BuiltinPortDef(string Name);
public record BuiltinControlDef(string Name, double Default);
```

For multi-input nodes the catalog entry describes the single-channel template
(`"In"`, `"Out"`). `BuiltinNodeViewModel` expands `"In"` → `"In 1"`, `"In 2"`,
… based on `ChannelCount`.

### `BuiltinNodeViewModel`

```csharp
public class BuiltinNodeViewModel(BuiltinNodeType type, int channelCount = 1)
    : NodeViewModelBase(
        BuiltinNodeCatalog.Get(type).DisplayName,
        /* inPorts  */ BuildInPorts(type, channelCount),
        /* outPorts */ BuildOutPorts(type, channelCount))
{
    public BuiltinNodeType NodeType { get; } = type;
    public int ChannelCount { get; } = channelCount;
    public IReadOnlyList<BuiltinControlDef> Controls { get; }
        = BuiltinNodeCatalog.Get(type).Controls;
}
```

Ports are plain `PortViewModelBase` instances (the same `InputPortViewModel` /
`OutputPortViewModel` already used by `InputNodeViewModel` and
`OutputNodeViewModel`). No LV2-specific subclass is needed.

### Toolbox section

`FilterGraphBuilderViewModel` gets a second observable collection:

```csharp
public ObservableCollection<BuiltinNodeGroup> AvailableBuiltins { get; }
```

Where `BuiltinNodeGroup` groups the 18 nodes into categories for the UI panel:

| Group | Nodes |
|---|---|
| Routing | Mixer, Copy, Multiply, Max |
| Math | Linear, Clamp, Reciprocal, Abs, Sqrt, Exp, Log |
| Generators | Sine, Ramp |
| Dynamics | Noise Gate, Zero Ramp, DC Block |
| Utility | Invert, Debug |

`AddBuiltinNode(BuiltinNodeType type, int channelCount)` mirrors the existing
`AddPlugin` command. For multi-channel nodes a small inline channel-count picker
appears in the toolbox entry (a `NumericUpDown` or `+`/`-` buttons bounded
1–8).

The toolbox panel in `FilterGraphBuilderView.axaml` (and
`FilterGraphBuilderWindow.axaml`) gains a second tab or accordion section
labelled "Built-in" alongside the existing LV2 plugin tree.

### `StorageConversionExtensions` additions

1. New `ToGrpc(this BuiltinNodeViewModel vm, Guid id) → FilterGraphBuiltinNode`
   — creates `FilterGraphBuiltinPort` entries from the view model ports,
   and `FilterGraphBuiltinControl` entries from the catalog defaults (overridden
   by any user-set values if a properties panel is added later).

2. `BaseToGrpc` switch gains a `BuiltinNodeViewModel` arm.

3. `IdInputPortByIndex` and `IdOutputPortByIndex` gain `FilterGraphBuiltinNode`
   arms (parallel to the existing `FilterGraphLv2Plugin` arms).

---

## What is explicitly out of scope here

- **Live control-port wiring**: connecting a `sine` output to a `linear` Mult
  input as a live audio-rate signal. This requires a control port type in the
  graph and a separate category of edge. Deferred.
- **Per-node properties panel**: a UI to edit control parameter values after
  node placement. Nodes use catalog defaults for now. The contract already
  stores them so this can be layered on later.
- **LADSPA, SOFA, ebur128 nodes**: out of scope; same pattern would apply.
