using Fr.Sonic.Model.Objects;

namespace Fr.Sonic.MidiPorts.Implementation;

internal record PendingLaunchpadMiniPort(
    ulong MidiPortId,
    string Tag,
    TaskCompletionSource<MidiPort> TaskCompletionSource,
    Port MiControllerInputPort,
    Port MiControllerOutputPort,
    Port DaControllerInputPort,
    Port DaControllerOutputPort)
{
    internal Node? MiSender { get; set; }
    internal Node? MiReceiver { get; set; }
    internal Node? DaSender { get; set; }
    internal Node? DaReceiver { get; set; }

    internal bool IsComplete =>
        MiSender is not null &&
        MiReceiver is not null &&
        DaSender is not null &&
        DaReceiver is not null;
}
