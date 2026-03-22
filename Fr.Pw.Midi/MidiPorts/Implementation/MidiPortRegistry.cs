using System.Collections.Concurrent;
using Fr.Pw.Midi.Mappings;

namespace Fr.Pw.Midi.MidiPorts.Implementation;

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