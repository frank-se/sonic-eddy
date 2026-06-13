using Fr.Sonic.Model.Objects;

namespace Fr.Sonic.Modules.Models;

public abstract record ThreeNodePipewireModule(
    string Name,
    string Tag,
    IntPtr ModuleHandle,
    ulong FirstNodeObjectSerial,
    ulong SecondNodeObjectSerial,
    ulong ThirdNodeObjectSerial)
    : PipewireModule(Name, Tag, ModuleHandle)
{
    public Node FirstNode =>
        FrSonic.NodeRegistry.GetByObjectSerial(FirstNodeObjectSerial)!;

    public Node SecondNode =>
        FrSonic.NodeRegistry.GetByObjectSerial(SecondNodeObjectSerial)!;

    public Node ThirdNode =>
        FrSonic.NodeRegistry.GetByObjectSerial(ThirdNodeObjectSerial)!;
}
