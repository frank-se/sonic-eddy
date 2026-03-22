namespace Fr.Pw.Midi.MidiPorts;

public interface IMidiPortFactory
{
    Task<MidiPort> CreateMidiMixPort();
}