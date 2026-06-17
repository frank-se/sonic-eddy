+++
title = "Beat Synchronization"
weight = 3
+++

Sonic Eddy provides a beat sync master, for use in its own tools, and by other
applications, either used directly, or with one of the beat sync conversion
capabilities described in the following sections.

## Synchronization

The synchronization protocol used by Sonic Eddy is described in the
[Sonic Eddy Synchronization Protocol Specification](https://git.sr.ht/~frank6/sonic-eddy/tree/main/item/docs/sync.md).
The synchronization master is controlled by the synchronization tool, accessible
under `Sync -> Synchronization ...` in the application menu.

![Synchronization Tool Window](/synchronization-tool.png)

## Midi Beat Clock

Sonic Eddy provides a midi beat clock node that converts the Sonic Eddy beat
clock to a midi beat clock, that can be sent to any midi port.

![Midi Sync Tool Window](/midi-sync-window.png)

## Click Track

Similarly to the midi beat clock node, Sonic Eddy also provides a click track
node that converts the Sonic Eddy beat clock to a click track, with run and
reset signals.

![Click Sync Tool Window](/click-sync-window.png)

> [!NOTE] The click track, and especially the run signal, works best with a
> dc-coupled audio interface.
