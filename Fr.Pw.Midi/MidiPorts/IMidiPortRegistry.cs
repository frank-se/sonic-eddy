using Fr.Pw.Midi.MidiPorts;

namespace Fr.Pw.Midi.Mappings;

public interface IMidiPortRegistry
{
    IReadOnlyCollection<MidiPort> MidiPorts { get; }
}