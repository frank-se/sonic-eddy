using System.Collections.Concurrent;
using Fr.Sonic.MidiPorts;

namespace Fr.Sonic.MidiPorts.Implementation;

public class MidiPortRegistry : IMidiPortRegistry
{
    public IReadOnlyCollection<MidiPort> MidiPorts =>
        (IReadOnlyCollection<MidiPort>)_midiPorts.Values;

    internal void AddPort(MidiPort port)
    {
        _midiPorts[port.Id] = port;
    }

    internal MidiPort? ById(ulong id) => _midiPorts.GetValueOrDefault(id);

    private readonly ConcurrentDictionary<ulong, MidiPort> _midiPorts = [];
}