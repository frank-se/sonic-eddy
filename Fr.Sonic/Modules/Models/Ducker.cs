using Fr.Sonic.Model.Objects;
using Fr.Sonic.PInvoke;

namespace Fr.Sonic.Modules.Models;

public record Ducker(
    string Name,
    string Tag,
    UIntPtr DuckerHandle,
    ulong MidiCaptureNodeObjectSerial,
    ulong AudioCaptureNodeObjectSerial,
    ulong AudioPlaybackNodeObjectSerial)
    : ThreeNodePipewireModule(Name, Tag,
        unchecked((IntPtr)(nuint)DuckerHandle),
        MidiCaptureNodeObjectSerial,
        AudioCaptureNodeObjectSerial,
        AudioPlaybackNodeObjectSerial)
{
    public Node MidiCaptureNode => FirstNode;
    public Node AudioCaptureNode => SecondNode;
    public Node AudioPlaybackNode => ThirdNode;

    public override void Destroy()
    {
        FrSonicLib.DestroyDuckerC(DuckerHandle);
        DeleteFromModuleRegistry();
    }
}
