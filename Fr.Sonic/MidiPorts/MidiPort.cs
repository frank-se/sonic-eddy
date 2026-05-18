using Fr.Sonic.PInvoke;
using Fr.Sonic.Model.Objects;

namespace Fr.Sonic.MidiPorts;

public record MidiPort(
    ulong Id,
    Node? Sender,
    Node Receiver);