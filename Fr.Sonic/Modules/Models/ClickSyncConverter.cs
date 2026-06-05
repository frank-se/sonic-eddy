using Fr.Sonic.PInvoke;

namespace Fr.Sonic.Modules.Models;

public sealed record ClickSyncConverter(
    string Name,
    string Tag,
    UIntPtr ConverterHandle,
    ulong ClickNodeObjectSerial,
    ulong ResetNodeObjectSerial,
    ulong RunNodeObjectSerial)
    : PipewireModule(Name, Tag,
        unchecked((IntPtr)(nuint)ConverterHandle))
{
    public Model.Objects.Node ClickNode =>
        FrSonic.NodeRegistry.GetByObjectSerial(ClickNodeObjectSerial)!;

    public Model.Objects.Node ResetNode =>
        FrSonic.NodeRegistry.GetByObjectSerial(ResetNodeObjectSerial)!;

    public Model.Objects.Node RunNode =>
        FrSonic.NodeRegistry.GetByObjectSerial(RunNodeObjectSerial)!;

    public override void Destroy()
    {
        FrSonicLib.DestroyClickSyncConverterC(ConverterHandle);
        DeleteFromModuleRegistry();
    }
}
