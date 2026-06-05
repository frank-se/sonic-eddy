using System.Collections.Concurrent;
using Fr.Sonic.PInvoke;
using Fr.Sonic.Factories;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Registries.Nodes;
using Fr.Sonic.Registries.Ports;

namespace Fr.Sonic.MidiPorts.Implementation;

public class MidiPortFactory : IMidiPortFactory, IDisposable
{
    internal MidiPortFactory(NodeRegistry nodeRegistry,
        MidiPortRegistry midiPortRegistry, ILinkFactory linkFactory,
        PortRegistry portRegistry, LayerSelectCallback layerSelectCallback,
        ChannelSelectCallback channelSelectCallback,
        DialModeCallback dialSectionModeSelectCallback,
        FilterSectionCallback filterParamsSectionSelectCallback,
        PagesRightCallback filterParamsSectionMovePagesRightCallback,
        PagesLeftCallback filterParamsSectionMovePagesLeftCallback,
        LaunchpadLoopPressedCallback launchpadLoopPressedCallback,
        LaunchpadFaderChangedCallback launchpadFaderChangedCallback)
    {
        nodeRegistry.Added += HandleNodeAddedEvent;
        _nodeRegistry = nodeRegistry;
        _midiPortRegistry = midiPortRegistry;
        _linkFactory = linkFactory;
        _portRegistry = portRegistry;
        _layerSelectCallback = layerSelectCallback;
        _channelSelectCallback = channelSelectCallback;
        _dialSectionModeSelectCallback = dialSectionModeSelectCallback;
        _filterParamsSectionSelectCallback = filterParamsSectionSelectCallback;
        _filterParamsSectionMovePagesRightCallback =
            filterParamsSectionMovePagesRightCallback;
        _filterParamsSectionMovePagesLeftCallback =
            filterParamsSectionMovePagesLeftCallback;
        _launchpadLoopPressedCallback = launchpadLoopPressedCallback;
        _launchpadFaderChangedCallback = launchpadFaderChangedCallback;
    }

    private readonly LayerSelectCallback _layerSelectCallback;
    private readonly ChannelSelectCallback _channelSelectCallback;
    private readonly DialModeCallback _dialSectionModeSelectCallback;
    private readonly FilterSectionCallback _filterParamsSectionSelectCallback;
    private readonly PagesRightCallback _filterParamsSectionMovePagesRightCallback;
    private readonly PagesLeftCallback _filterParamsSectionMovePagesLeftCallback;
    private readonly LaunchpadLoopPressedCallback _launchpadLoopPressedCallback;
    private readonly LaunchpadFaderChangedCallback _launchpadFaderChangedCallback;

    private void HandleNodeAddedEvent(Node? node)
    {
        if (node is null) return;

        if (node.Pmx.Purpose != FrSonicMidi.PmxPurpose) return;

        var tag = node.Pmx.Tag;

        if (tag is null)
            throw new ApplicationException("Tag is null but purpose is set!");

        if (_pendingLaunchpadMiniPorts.TryGetValue(tag, out var launchpadPending))
        {
            HandleLaunchpadMiniNodeAdded(node, launchpadPending);
            return;
        }

        MidiPort? midiPort;
        TaskCompletionSource<MidiPort>? tcs;
        Port? inputPort;
        Port? outputPort;

        using (_updatePendingLock.EnterScope())
        {
            if (!_pendingMidiPorts.TryGetValue(tag, out var pending))
            {
                throw new ApplicationException(
                    $"Purpose was {FrSonicMidi.PmxPurpose}, but there was no pending midi port!");
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

            if (pending.Receiver is null || (pending.Sender is null &&
                                             pending.ControllerInputPort is not
                                                 null))
                return;

            _pendingMidiPorts.TryRemove(tag, out _);

            midiPort = new(pending.MidiPortId, pending.Sender,
                pending.Receiver);

            tcs = pending.TaskCompletionSource;

            inputPort = pending.ControllerInputPort;
            outputPort = pending.ControllerOutputPort;
        }

        tcs.TrySetResult(midiPort);
        _midiPortRegistry.AddPort(midiPort);

        var myInputPort =
            _portRegistry.Objects.FirstOrDefault(p =>
                p.Node.Id == midiPort.Receiver.ObjectId);

        if (myInputPort is null)
            throw new ApplicationException(
                $"Couldn't find my input port for node id {midiPort.Receiver.ObjectId}");

        _linkFactory.CreateLink(outputPort, myInputPort);

        if (midiPort.Sender is null || inputPort is null) return;

        var myOutputPort =
            _portRegistry.Objects.FirstOrDefault(p =>
                p.Node.Id == midiPort.Sender.ObjectId);

        if (myOutputPort is null)
            throw new ApplicationException(
                $"Couldn't find my output port for node id {midiPort.Sender.ObjectId}");

        _linkFactory.CreateLink(myOutputPort, inputPort);
    }

    private void HandleLaunchpadMiniNodeAdded(Node node,
        PendingLaunchpadMiniPort pending)
    {
        MidiPort? midiPort = null;

        using (_updatePendingLock.EnterScope())
        {
            switch (node)
            {
                case { Media.Class: "Stream/Output/Midi", Name: string name }
                    when name.Contains("Launchpad Mini MI"):
                    pending.MiSender = node;
                    break;
                case { Media.Class: "Stream/Output/Midi", Name: string name }
                    when name.Contains("Launchpad Mini DA"):
                    pending.DaSender = node;
                    break;
                case { Media.Class: "Stream/Input/Midi", Name: string name }
                    when name.Contains("Launchpad Mini MI"):
                    pending.MiReceiver = node;
                    break;
                case { Media.Class: "Stream/Input/Midi", Name: string name }
                    when name.Contains("Launchpad Mini DA"):
                    pending.DaReceiver = node;
                    break;
            }

            if (!pending.IsComplete)
                return;

            _pendingLaunchpadMiniPorts.TryRemove(pending.Tag, out _);
            midiPort = new(pending.MidiPortId, pending.DaSender,
                pending.DaReceiver!);
        }

        var miInput = GetNodePort(pending.MiReceiver!);
        var daInput = GetNodePort(pending.DaReceiver!);
        var miOutput = GetNodePort(pending.MiSender!);
        var daOutput = GetNodePort(pending.DaSender!);

        _linkFactory.CreateLink(pending.MiControllerOutputPort, miInput);
        _linkFactory.CreateLink(pending.DaControllerOutputPort, daInput);
        _linkFactory.CreateLink(miOutput, pending.MiControllerInputPort);
        _linkFactory.CreateLink(daOutput, pending.DaControllerInputPort);

        pending.TaskCompletionSource.TrySetResult(midiPort);
        _midiPortRegistry.AddPort(midiPort);
    }

    public Task<MidiPort> CreateMidiMixPort()
    {
        var (inputPort, outputPort) =
            GetPotentialPorts("MIDI Mix:MIDI Mix MIDI");

        var tag = GenerateTag();

        var id = FrSonicLib.CreateMidiMixPortC(FrSonicMidi.PmxPurpose, tag,
            _layerSelectCallback, _channelSelectCallback,
            _dialSectionModeSelectCallback,
            _filterParamsSectionSelectCallback);

        var pending =
            new PendingMidiPort(id, tag, new(), inputPort, outputPort);

        _pendingMidiPorts[tag] = pending;

        return pending.TaskCompletionSource.Task;
    }

    public Task<MidiPort> CreateCmdMm1Port()
    {
        var (inputPort, outputPort) =
            GetPotentialPorts("CMD MM-1:CMD MM-1 MIDI");

        var tag = GenerateTag();

        var id = FrSonicLib.CreateMm1PortC(FrSonicMidi.PmxPurpose, tag,
            _layerSelectCallback, _channelSelectCallback,
            _dialSectionModeSelectCallback,
            _filterParamsSectionSelectCallback,
            _filterParamsSectionMovePagesRightCallback,
            _filterParamsSectionMovePagesLeftCallback);

        var pending =
            new PendingMidiPort(id, tag, new(), inputPort, outputPort);

        _pendingMidiPorts[tag] = pending;

        return pending.TaskCompletionSource.Task;
    }

    public Task<MidiPort> CreateFaderFoxPc4Port()
    {
        var outputPort = GetOutputPort("Faderfox PC4:Faderfox PC4 MIDI");

        var tag = GenerateTag();

        var id = FrSonicLib.CreateFaderFoxPc4PortC(FrSonicMidi.PmxPurpose, tag);

        var pending =
            new PendingMidiPort(id, tag, new(), null, outputPort);

        _pendingMidiPorts[tag] = pending;

        return pending.TaskCompletionSource.Task;
    }

    public Task<MidiPort> CreateLaunchpadMiniPort()
    {
        var (miInput, miOutput) = GetPotentialPorts(
            "Launchpad Mini MK3:Launchpad Mini MK3 LPMiniMK3 MI");
        var (daInput, daOutput) = GetPotentialPorts(
            "Launchpad Mini MK3:Launchpad Mini MK3 LPMiniMK3 DA");

        var tag = GenerateTag();

        var id = FrSonicLib.CreateLaunchpadMiniPortC(FrSonicMidi.PmxPurpose,
            tag, _layerSelectCallback, _launchpadLoopPressedCallback,
            _launchpadFaderChangedCallback);

        var pending = new PendingLaunchpadMiniPort(id, tag, new(), miInput,
            miOutput, daInput, daOutput);

        _pendingLaunchpadMiniPorts[tag] = pending;

        return pending.TaskCompletionSource.Task;
    }

    private Port GetNodePort(Node node)
    {
        var port = _portRegistry.Objects.FirstOrDefault(p =>
            p.Node.Id == node.ObjectId);
        if (port is null)
            throw new ApplicationException(
                $"Couldn't find port for node id {node.ObjectId}");

        return port;
    }

    private (Port input, Port output) GetPotentialPorts(string portAlias)
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
                           alias.Contains(portAlias);
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

        return (inputPort, outputPort);
    }

    private Port GetOutputPort(string portAlias)
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
                           alias.Contains(portAlias);
                }
            ).ToArray();

        var outputPort =
            potentialPorts.FirstOrDefault(p => p.Direction == "out");

        if (outputPort is null)
            throw new ApplicationException("No midi output port found");

        return outputPort;
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

    private readonly ConcurrentDictionary<string, PendingLaunchpadMiniPort>
        _pendingLaunchpadMiniPorts = [];
}
