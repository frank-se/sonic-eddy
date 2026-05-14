# Getting Started

The API gives access to everything with the static class `Wireplumber`. To use the
API, first initialize it with `Wireplumber.Start()`, to stop, execute
`Wireplumber.Stop()`. `Wireplumber.Start()` will start the wireplumber, and pipewire
loops, as well as the threads used to process the pipewire events. Once started, it
listens to pipewire events, and forward the events to the specific registries, and it
triggers the provided events.

## A quick Introduction of Pipewire

Given that the API heavily depends on pipewire and wireplumber, it seems prudent to
provide a quick overview of pipewire. Pipewire is an audio and video server for Linux,
and as such, it provides application access to audio devices, video devices, filters,
and audio from other applications.
I was started by Wym Taymans, and is based on learnings from PulseAudio, gstreamer, and
the Jack Audio connection kit. It provides a backend that manages pipewire objects.
Client connect to the backend to interact with pipewire, and it provides methods to
manage those objects. Additionally, pipewire provides and API to interact with the
backend, implement filters and streams, and the Simple Plugin API (SPA) which is used
to implement the core processing.
It also provides command line tools to interact with the pipewire daemon.
Otherwise, pipewire doesn't provide a lot of functionality. It doesn't connect nodes,
it doesn't create and nodes, starting just a pipewire daemon leaves us with an empty
daemon, no objects, or anything else will be running there.
This is where wireplumber, the session manager for pipewire comes in. Wireplumber
connects to pipewire, listens to objects and creates the necessary connections. For
example, it allows the selection of a default audio device, and automatically connects
an application that wants to play audio to the default audio device.

## How FrWireplumber relates to pipewire and wireplumber

FrWireplumber bridges the c APIs to interact with wireplumber and pipewire to a hopefully
easy to use, C# object model. There is a `Node` to interact with pipewire nodes, there's a
device to interact with a pipewire `Device`, and so on.
Registries provide access to the objects. Each object has its own registry, a `NodeRegistry`
to get access to `Nodes`, a `DeviceRepository` to get access to a `Device`, and so on.
The `IModuleFactory` allows the creation of modules. Each method takes a module config,
which is forwards to the pipewire module factory, and then asynchronously waits for pipewire
to create the modules nodes, and inform us about the new nodes. When the nodes are registered
in the node registry it finishes the task, and returns a module.
The static class `Wireplumber` gives access to the registries, and the factories.

## Examples

To run the examples, add the `FrWireplumber` package `dotnet add package FrWireplumber`.

### Changing the volume of a node

```csharp
Wireplumber.Start();

// wait for the node to be available in the registry
await Task.Delay(TimeSpan.FromSeconds(2));

// get the node from the registry
var node = await Wireplumber.NodeRegistry.GetByObjectSerial(213);

// Set the volumes of the node, in this case 2 channels
node.SetVolumes[[0.4, 0.4]];

Wireplumber.Stop();
```