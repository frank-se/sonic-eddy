using Fr.Sonic.Model.Objects;

namespace Fr.Sonic.MidiPorts.Implementation;

internal record PendingMidiPort(
    ulong MidiPortId,
    string Tag,
    TaskCompletionSource<MidiPort> TaskCompletionSource,
    Port? ControllerInputPort,
    Port ControllerOutputPort)
{
    internal Node? Sender { get; set; }
    internal Node? Receiver { get; set; }
}