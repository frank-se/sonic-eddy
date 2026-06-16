# Jack Compatibility features

## Jack Input Ports

Jack ports are not auto routed with wireplumber. Sonic Eddy provides jack input
ports to circumvent this issue. Jack Input Ports are created in
`Audio -> Jack Input Ports`. Once a jack input port is created, Sonic Eddy
creates a loopback module. Both, the playback, and the capture node of the
module need to linger (`linger = true`).

The playback node will be routed using wireplumber, and will only be used as the
target object of a normal audio channel. It needs to show up in the audio from
selection combo box of normal channels.

The capture nodes on the other hand, need to be managed by Sonic Eddy, because
wireplumber doesn't link nodes using the jack client at all. Sonic Eddy stores
the target node name and the port mappings for every loopback module, and when a
jack node, or the capture port of the jack input port show up, Sonic Eddy needs
to create the links.

> [!NOTE] Jack nodes are best identified by the `client.api` of the node, which
> will be `jack` for a jack client. The attribute probably needs to be added to
> the pipewire object properties propagation

## User Interface

The user interface is a window, accessible through the Audio menu. It shows a
list of jack ports, and allows adding, and deleting them. It shows all ports,
even the ones not connected, but shows the connection state for every port.
