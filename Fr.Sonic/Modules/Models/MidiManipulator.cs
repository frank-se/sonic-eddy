using Fr.Sonic.PInvoke;

namespace Fr.Sonic.Modules.Models;

public record MidiManipulator(
    string Name,
    string Tag,
    UIntPtr ManipulatorHandle,
    ulong CaptureNodeObjectSerial,
    ulong PlaybackNodeObjectSerial)
    : TwoNodePipewireModule(Name, Tag,
        unchecked((IntPtr)(nuint)ManipulatorHandle),
        CaptureNodeObjectSerial, PlaybackNodeObjectSerial)
{
    public new void Destroy()
    {
        FrSonicLib.DestroyMidiManipulatorC(ManipulatorHandle);
        DeleteFromModuleRegistry();
    }
}
