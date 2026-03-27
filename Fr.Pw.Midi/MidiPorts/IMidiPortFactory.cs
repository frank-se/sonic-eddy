namespace Fr.Pw.Midi.MidiPorts;

public interface IMidiPortFactory
{
    Task<MidiPort> CreateMidiMixPort();
    Task<MidiPort> CreateCmdMm1Port();
    Task<MidiPort> CreateFaderFoxPc4Port();
}