using Fr.Sonic.MidiPorts;

namespace Fr.Sonic.MidiPorts;

public interface IMidiPortRegistry
{
    IReadOnlyCollection<MidiPort> MidiPorts { get; }
}