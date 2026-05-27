using Fr.Sonic.Model.Objects;

namespace Fr.Sonic.Modules.Models.Pending;

internal abstract class PendingTwoNodeModule(string tag) : PendingModule(tag)
{
    protected Node? CaptureNode;
    protected Node? PlaybackNode;
    internal IntPtr ModuleHandle = IntPtr.Zero;
    private bool _moduleHandleReady;

    protected bool CanBeCompleted => CaptureNode is not null &&
                                     PlaybackNode is not null &&
                                     _moduleHandleReady;

    internal void SetCaptureNode(Node node)
    {
        using (Sync.EnterScope())
        {
            CaptureNode = node;
        }
    }

    internal void SetPlaybackNode(Node node)
    {
        using (Sync.EnterScope())
        {
            PlaybackNode = node;
        }
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
