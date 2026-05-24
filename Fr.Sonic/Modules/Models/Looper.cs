using Fr.Sonic.PInvoke;

namespace Fr.Sonic.Modules.Models;

public record Looper(
    string Name,
    string Tag,
    UIntPtr LooperHandle,
    ulong CaptureNodeObjectSerial,
    ulong PlaybackNodeObjectSerial)
    : TwoNodePipewireModule(Name, Tag,
        unchecked((IntPtr)(nuint)LooperHandle),
        CaptureNodeObjectSerial, PlaybackNodeObjectSerial)
{
    public new void Destroy()
    {
        FrSonicLib.DestroyLooperC(LooperHandle);
        DeleteFromModuleRegistry();
    }
}
