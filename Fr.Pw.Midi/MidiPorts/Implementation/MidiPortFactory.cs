using System.Collections.Concurrent;
using Fr.Pw.Midi.PInvoke;
using Fr.Wireplumber.Factories;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Registries.Nodes;
using Fr.Wireplumber.Registries.Ports;

namespace Fr.Pw.Midi.MidiPorts.Implementation;

public class MidiPortFactory : IMidiPortFactory, IDisposable
{
    public MidiPortFactory(NodeRegistry nodeRegistry,
        MidiPortRegistry midiPortRegistry, ILinkFactory linkFactory,
        PortRegistry portRegistry)
    {
        nodeRegistry.Added += HandleNodeAddedEvent;
        _nodeRegistry = nodeRegistry;
        _midiPortRegistry = midiPortRegistry;
        _linkFactory = linkFactory;
        _portRegistry = portRegistry;
    }

    private void HandleNodeAddedEvent(Node? node)
    {
        if (node is null) return;

        if (node.Pmx.Purpose != FrPwMidiLib.PMX_PURPOSE) return;

        var tag = node.Pmx.Tag;

        if (tag is null)
            throw new ApplicationException("Tag is null but purpose is set!");

        MidiPort? midiPort;
        TaskCompletionSource<MidiPort>? tcs;
        Port? inputPort;
        Port? outputPort;

        using (_updatePendingLock.EnterScope())
        {
            if (!_pendingMidiPorts.TryGetValue(tag, out var pending))
            {
                throw new ApplicationException(
                    $"Purpose was {FrPwMidiLib.PMX_PURPOSE}, but there was no pending midi port!");
            }

            switch (node.Media)
            {
                case { Class: "Stream/Output/Midi" }:
                    pending.Sender = node;
                    break;
                case { Class: "Stream/Input/Midi" }:
                    pending.Receiver = node;
                    break;
            }

            if (pending.Receiver is null || pending.Sender is null) return;

            _pendingMidiPorts.TryRemove(tag, out _);

            midiPort = new(pending.MidiPortId, pending.Sender,
                pending.Receiver);

            tcs = pending.TaskCompletionSource;

            inputPort = pending.InputPort;
            outputPort = pending.OutputPort;
        }

        tcs.TrySetResult(midiPort);
        _midiPortRegistry.AddPort(midiPort);

        var myInputPort =
            _portRegistry.Objects.FirstOrDefault(p =>
                p.Node.Id == midiPort.Receiver.ObjectId);

        if (myInputPort is null)
            throw new ApplicationException(
                $"Couldn't find my input port for node id {midiPort.Receiver.ObjectId}");

        var myOutputPort =
            _portRegistry.Objects.FirstOrDefault(p =>
                p.Node.Id == midiPort.Sender.ObjectId);

        if (myOutputPort is null)
            throw new ApplicationException(
                $"Couldn't find my output port for node id {midiPort.Sender.ObjectId}");

        _linkFactory.CreateLink(myOutputPort, inputPort);
        _linkFactory.CreateLink(outputPort, myInputPort);
    }

    public Task<MidiPort> CreateMidiMixPort()
    {
        var targetNode = _nodeRegistry.Objects.FirstOrDefault(n =>
            n is { Name: "Midi-Bridge", Media.Class: "Midi/Bridge" });

        if (targetNode is null)
            throw new ApplicationException("Midi bridge not found");

        var potentialPorts =
            _portRegistry.Objects.Where(p =>
                {
                    var alias = p.Alias;
                    if (alias is null) return false;
                    return p.Node.Id == targetNode.ObjectId &&
                           alias.Contains("MIDI Mix:MIDI Mix MIDI");
                }
            ).ToArray();

        var inputPort =
            potentialPorts.FirstOrDefault(p => p.Direction == "in");

        if (inputPort is null)
            throw new ApplicationException("No midi input port found");

        var outputPort = potentialPorts.FirstOrDefault(p =>
            p.Direction == "out" && p.Alias == inputPort.Alias);

        if (outputPort is null)
            throw new ApplicationException("No midi output port found");

        var tag = GenerateTag();

        var id = FrPwMidiLib.CreateMidiMixPort(tag,
            LayerSelectCallback, ChannelSelectCallback,
            DialSectionModeSelectionCallback,
            FilterParamsSectionSelectCallback);

        var pending =
            new PendingMidiPort(id, tag, new(), inputPort, outputPort);

        _pendingMidiPorts[tag] = pending;

        return pending.TaskCompletionSource.Task;
    }

    private void LayerSelectCallback(ulong midiPortId, ulong layerId)
    {
        var port = _midiPortRegistry.ById(midiPortId);

        if (port is null)
        {
            Console.WriteLine(
                $"ERROR: Received callback for unknown midi port {midiPortId}");
            return;
        }

        port.TriggerLayerSelectedEvent(layerId);
    }

    private void ChannelSelectCallback(ulong midiPortId, ulong channelId)
    {
        var port = _midiPortRegistry.ById(midiPortId);

        if (port is null)
        {
            Console.WriteLine("ERROR: Received callback for unknown midi port");
            return;
        }

        port.TriggerChannelSelectedEvent(channelId);
    }

    private void DialSectionModeSelectionCallback(ulong midiPortId,
        ulong channelId, DialMode dialMode)
    {
        var port = _midiPortRegistry.ById(midiPortId);

        if (port is null)
        {
            Console.WriteLine("ERROR: Received callback for unknown midi port");
            return;
        }

        port.TriggerDialSelectionModeChangedEvent(channelId, dialMode);
    }

    private void FilterParamsSectionSelectCallback(ulong midiPortId,
        ulong channelId, ulong sectionId)
    {
        var port = _midiPortRegistry.ById(midiPortId);

        if (port is null)
        {
            Console.WriteLine("ERROR: Received callback for unknown midi port");
            return;
        }

        port.TriggerFilterParamsSectionChangedEvent(channelId, sectionId);
    }

    public void Dispose()
    {
        _nodeRegistry.Added += HandleNodeAddedEvent;
        GC.SuppressFinalize(this);
    }

    private static string GenerateTag() => Guid.NewGuid().ToString("N")[^12..];

    private readonly NodeRegistry _nodeRegistry;
    private readonly ILinkFactory _linkFactory;
    private readonly MidiPortRegistry _midiPortRegistry;
    private readonly PortRegistry _portRegistry;
    private readonly Lock _updatePendingLock = new();

    private readonly ConcurrentDictionary<string, PendingMidiPort>
        _pendingMidiPorts = [];
}