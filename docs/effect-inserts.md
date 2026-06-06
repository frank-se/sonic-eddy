# External Effect Inserts

The mixer currently supports filter chains with dsp plugins for audio
processing. We want to add the ability to integrate external effects through
external effect inserts.

An external effect insert consists of two loopback modules, the input loopback,
responsible for passing data to the external effect, and the output loopback,
responsible for receiving the returned audio from the effect.

External effect inserts should be usable where ever a filter chain can currently
be inserted in the mixer.

> [!NOTE] We are explicitly ignoring sample delay caused by the inserted effects

## Configuration And Persistence

External effects are persisted independently from mixer configurations. Each
external effect definition contains:

- A stable ID
- A display name
- The external effect input node and selected input ports
- The external effect output node and selected output ports
- The channel mapping for both directions

Node and port names are persisted instead of transient PipeWire object IDs.
Stereo effects use `[FL, FR]` unless a different mapping is explicitly
configured.

If either configured external endpoint is unavailable, the effect remains
configured but unavailable. No replacement endpoint is selected automatically.
When an unavailable effect is assigned to an insertion point, the signal path is
interrupted; Sonic Eddy does not bypass the effect automatically. The management
screen and insertion-point UI must show that the effect is unavailable.

## Input loopback

The input loopback carries audio from the mixer insertion input to the external
effect inputs:

```text
Mixer insertion input -> Input loopback -> External effect inputs
```

- The input loopback capture node targets the playback node at the mixer
  insertion input.
- The input loopback playback node targets the configured external effect input
  node and ports using `target.object` and `audio.position`.
- The input loopback capture node provides audio positions `[FL, FR]`.

## Output loopback

The output loopback carries audio from the external effect outputs to the mixer
insertion output:

```text
External effect outputs -> Output loopback -> Mixer insertion output
```

- The output loopback capture node targets the configured external effect output
  node and ports using `target.object` and `audio.position`.
- The output loopback playback node targets the capture node at the mixer
  insertion output.
- The output loopback playback node provides audio positions `[FL, FR]`.

## User Interface

External effects will have their own management screen at
`Tools -> External Effects`. The screen list all defined existing effects, and
allows the creation of new effects.

External effects can be picked where ever a filter chain could normally be
inserted.

The management screen initially provides simple create, edit, and delete
operations. Editing or deleting an effect that is currently assigned to an
insertion point is not allowed. Each effect displays its availability and a
`Used By` value identifying its current insertion point.

An external effect can be assigned to at most one insertion point at a time.
Effects already in use are disabled in other insertion-point selectors.
If persisted configuration assigns the same external effect to multiple
insertion points, mixer restoration fails with an exception so the invalid
configuration is found immediately.

Node and port selectors list external audio endpoints at port level and exclude
all nodes created by Sonic Eddy. External-effect loopbacks are created through
the existing module factory and receive `pmx.tag`, so they are identified as
internal nodes by the existing filtering mechanism.

An external effect requires exactly two input ports and two output ports. Port
selection order maps to `[FL, FR]`: the first selected port is `FL` and the
second selected port is `FR`.

## Insert Processor Model

Filter chains and external effect inserts are variants of the same insert
processor concept. Every insertion point has exactly one of:

```text
None
Filter chain
External effect
```

Mixer state persists the selected processor type and either the filter graph ID
and parameter values or the external effect ID. This model must be extensible so
additional processor types can be added later without duplicating insertion
logic throughout channel, group, return, master, UI, and persistence code.

External effect inserts are available at every insertion point where a filter
chain can currently be selected. Channel and group send taps use the output of
the selected processor, preserving the current post-effect send behavior.

Replacing an insert processor follows this order:

1. Create and validate the new processor.
2. Retarget the insertion input, insertion output, and post-effect taps to the
   new processor.
3. Destroy the previous processor.

Removing a processor restores the direct path before destroying the processor.
The two loopback modules belonging to an external insert are created and
destroyed together.

## Master Channel Changes

In order to be able to use external effect inserts for the master channel, we
will add the ability to add filter chains or external effect inserts behind the
global master playback node. The playback node of the filter chain or external
effect insert will take the target object of the global master node instead.

The resulting topology is:

```text
Global master playback -> Insert processor -> Physical master output
```

The global master playback node targets the selected processor's input node.
The processor's output node targets the configured physical master output. If
no processor is selected, the global master playback node targets the physical
output directly. The selected post-global-master processor is persisted as part
of mixer state.
