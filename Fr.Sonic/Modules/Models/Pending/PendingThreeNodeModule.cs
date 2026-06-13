using Fr.Sonic.Model.Objects;

namespace Fr.Sonic.Modules.Models.Pending;

internal abstract class PendingThreeNodeModule(string tag) : PendingModule(tag)
{
    protected Node? FirstNode;
    protected Node? SecondNode;
    protected Node? ThirdNode;
    internal IntPtr ModuleHandle = IntPtr.Zero;
    private bool _moduleHandleReady;

    protected bool CanBeCompleted =>
        FirstNode is not null && SecondNode is not null &&
        ThirdNode is not null && _moduleHandleReady;

    internal void SetFirstNode(Node node)
    {
        using (Sync.EnterScope())
            FirstNode = node;
    }

    internal void SetSecondNode(Node node)
    {
        using (Sync.EnterScope())
            SecondNode = node;
    }

    internal void SetThirdNode(Node node)
    {
        using (Sync.EnterScope())
            ThirdNode = node;
    }

    internal void SetModuleHandle(IntPtr moduleHandle)
    {
        using (Sync.EnterScope())
        {
            ModuleHandle = moduleHandle;
            _moduleHandleReady = true;
        }
    }
}
