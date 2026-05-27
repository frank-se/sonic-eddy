# Filter Graph Editor

The filter graph editor lets users build LV2 plugin chains that become PipeWire
`filter-chain` modules. The editor loads all installed LV2 plugins, lets users
place them as nodes on a canvas, and connects their audio ports with edges.
The resulting graph is serialized to a `.fc` file and applied to a channel strip
via `MixerEditor`.

## Current state

The existing implementation works but is constrained:

- `GraphEditor` is a `Canvas` with a fixed size equal to the window.
- Nodes are placed in a diagonal cascade; there is no way to scroll to a node
  that falls outside the visible area.
- Edges are `Line` controls positioned in screen coordinates. When the canvas
  has no transform, this is fine. When panning or zooming it breaks.
- The input and output nodes are pinned to the left/right edges of the
  `GraphEditor` control by reacting to `SizeChanged`.

These constraints make graphs with more than a handful of plugins awkward. The
overhaul introduces a proper canvas/viewport split as the foundation for future
improvements.

---

## Phase 1 — Infinite canvas with pan and scroll

### Goals

- The canvas grows to contain all nodes; it is never artificially bounded by
  the window size.
- The user can pan to any part of the canvas by dragging the background or
  using scrollbars.
- Node drag, edge creation, and port hit-testing all continue to work
  correctly after a pan.
- Edges track their endpoint nodes precisely in canvas space.
- Scrollbars give an at-a-glance sense of where in the canvas the viewport
  sits.
- The Input and Output nodes remain reachable but are no longer pinned to the
  screen edges; they have stable canvas positions instead.

### Non-Goals for phase 1

- Zoom / scale transform.
- Minimap.
- Snapping nodes to a grid.
- Undo/redo.
- Multi-node selection or marquee drag.

---

### Coordinate spaces

Two coordinate spaces exist after this change:

| Space | Description |
|---|---|
| **Canvas space** | Logical coordinates where nodes live. Node positions, port centers, and edge endpoints are all expressed here. |
| **Viewport space** | Screen pixel coordinates relative to the `GraphEditorViewport` control. Mouse events arrive here. |

The relationship between them is a pure translation:

```
canvas_point = viewport_point - pan_offset
viewport_point = canvas_point + pan_offset
```

Phase 1 has no scale factor. Adding zoom later just multiplies:

```
canvas_point = (viewport_point - pan_offset) / zoom
```

All existing code that computes `GetConnectionCenterPoint(this)` passes the
`GraphEditor` as the reference control. After the split, port centers must be
computed in canvas space and converted to viewport space for rendering, or the
rendering layer must apply the same transform to both nodes and edges.

---

### Control structure

The current `GraphEditor : Canvas` is split into two controls:

**`GraphEditorCanvas : Canvas`** — the inner, logically infinite canvas.

- Holds all `NodeControl`, `InputsControl`, `OutputsControl`, and edge `Line`
  children.
- Has no clipping. Its `Width` and `Height` grow to encompass all node
  positions plus a fixed margin (e.g., 200 px on each side beyond the bounding
  box of all nodes).
- Does not handle pan input directly; it is moved by the viewport.
- Applies no transform of its own. Its position relative to the viewport is
  set by the viewport via `Canvas.SetLeft` / `Canvas.SetTop` on the inner
  canvas, or via a `TranslateTransform`.

**`GraphEditorViewport : UserControl`** (replaces `GraphEditor`)

- Clips its content to its own bounds.
- Contains the `GraphEditorCanvas` as a child.
- Owns the pan offset (`_panX`, `_panY`) as private state.
- Handles background pointer events for panning (middle-button drag, or
  space+left-button drag).
- Hosts horizontal and vertical `ScrollBar` controls overlaid at the edges of
  the viewport. The scrollbars reflect the current pan offset and canvas size,
  and moving them updates the pan offset.
- Exposes the same `GraphNodes`, `GraphEdges`, `GraphInputs`, `GraphOutputs`,
  `CreateEdgeCommand`, and `DeleteEdgeCommand` properties as the current
  `GraphEditor` so the AXAML binding in `FilterGraphBuilderView` needs minimal
  changes.

The existing `GraphEditor.cs` can be renamed to `GraphEditorViewport.cs` and
its inner canvas work extracted into `GraphEditorCanvas.cs`.

---

### Pan behavior

**Middle-button drag** pans the canvas. While the middle button is held:

1. Capture the pointer on the viewport.
2. On each `PointerMoved`, compute the delta from the last position and add it
   to `(_panX, _panY)`.
3. Apply the new pan by repositioning the inner canvas inside the viewport.
4. Update the scrollbars to reflect the new position.

**Scrollbar interaction** updates the pan offset directly:

```
_panX = scrollbarX.Value
_panY = scrollbarY.Value
```

The scrollbar `Maximum` is `canvasWidth - viewportWidth` (clamped to zero when
the canvas fits inside the viewport). `ViewportSize` is set to the viewport
dimension so the thumb size scales correctly.

**Clamping**: pan offset is clamped so the viewport never scrolls past the
canvas bounds. When nodes are added and the canvas grows, the maximum is
updated and clamping is reapplied.

---

### Canvas size tracking

The `GraphEditorCanvas` computes its own size after any node is added, moved,
or removed:

1. Iterate all `NodeControl` children.
2. Find the bounding box: `minX = min(Canvas.GetLeft)`, `minY`, `maxX`, `maxY`
   (including node width/height once measured).
3. Set `Width = maxX - minX + 2 * Margin` and `Height = maxY - minY + 2 *
   Margin` where `Margin = 200`.
4. If `minX < 0` or `minY < 0`, shift all children by the negative amount to
   keep all positions non-negative (canvas origin never goes below zero).

This is called on `NodeControl` header release (end of drag) and when a node
is added. The viewport reacts to `GraphEditorCanvas` size changes by
recalculating scrollbar ranges.

---

### Node positioning

New nodes are currently placed in a diagonal cascade using a static field.
After this change:

- The initial position for a new node is the center of the current viewport
  (converted to canvas space), offset by a small amount to avoid exact overlap
  with any node already there.
- Node positions are stored on the view model (`NodeViewModelBase.CanvasX`,
  `NodeViewModelBase.CanvasY`) so they survive serialization in a future
  iteration.
- The Input node is placed at canvas coordinate `(60, canvasHeight / 2)` when
  the graph is created. The Output node is placed at `(canvasWidth - 200,
  canvasHeight / 2)`. Both are draggable with the same header-drag behavior as
  plugin nodes.

---

### Edge rendering

Edges are currently `Line` controls with `StartPoint` and `EndPoint` set in
the coordinate space of the `GraphEditor` canvas. This must remain true of the
`GraphEditorCanvas` — edges live on the inner canvas, not on the viewport.

`UpdateConnectionsForNodeMove` and `StartConnectionOperation` already compute
port centers via `portControl.GetConnectionCenterPoint(referenceControl)`.
After the split, `referenceControl` becomes the `GraphEditorCanvas`, not the
viewport. The transform between the two is a pure translation, so no math
changes — just the reference control passed to `TranslatePoint` changes.

The in-progress edge line during a connection drag is also added to the
`GraphEditorCanvas`. Mouse positions during the drag must be converted from
viewport space to canvas space before being assigned to `_newConnectionLine.EndPoint`.

---

### Port hit-testing during connection drag

`FindSelectedPort` currently iterates all input ports and measures the
Euclidean distance between the port center and the pointer position, both in
`GraphEditor` (screen) coordinates. After the split, both must be in canvas
coordinates. The pointer position from the event must be converted:

```csharp
var canvasPosition = e.GetPosition(_canvas);
```

because events are delivered to the viewport and `GetPosition(_canvas)` already
returns the correct canvas-space point when the canvas is a visual child of the
viewport.

---

### Scrollbar layout

The viewport uses an `Avalonia.Layout` overlay approach:

```
GraphEditorViewport (UserControl, clips)
├── GraphEditorCanvas          (absolute-positioned inner canvas)
├── ScrollBar (Horizontal)     (docked Bottom, z-order above canvas)
└── ScrollBar (Vertical)       (docked Right, z-order above canvas)
```

The corner where the two scrollbars meet gets a small filler rectangle.
Scrollbars use `Opacity = 0.85` so they float visually over the canvas rather
than resizing it.

---

### AXAML changes

`FilterGraphBuilderView.axaml` changes only the element name:

```xml
<!-- before -->
<graphEditorControl:GraphEditor ... />

<!-- after -->
<graphEditorControl:GraphEditorViewport ... />
```

All bound properties keep the same names.

---

## Phase 2 — Zoom (future)

Zoom extends the coordinate transform to include a scale factor. The
`GraphEditorCanvas` gets a `ScaleTransform` (or a `MatrixTransform`) applied.
All coordinate conversions gain a `/ zoom` term. Port hit distances must also
scale: a 10 px snap radius in viewport space stays 10 px regardless of zoom,
so the canvas-space radius becomes `10 / zoom`.

Zoom input: Ctrl+scroll wheel on the viewport, centered on the pointer position
(zoom toward cursor, not toward origin).

## Phase 3 — Minimap (future)

A small thumbnail in a corner of the viewport renders a scaled-down version of
the entire canvas. The visible region is overlaid as a rectangle. Dragging the
rectangle pans the viewport.
