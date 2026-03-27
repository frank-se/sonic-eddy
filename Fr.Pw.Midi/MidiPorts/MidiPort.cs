using Fr.Pw.Midi.PInvoke;
using Fr.Wireplumber.Model.Objects;

namespace Fr.Pw.Midi.MidiPorts;

public record MidiPort(
    ulong Id,
    Node? Sender,
    Node Receiver);