using Fr.Wireplumber.Model.Objects;

namespace Fr.Wireplumber.Modules.Models.Pending;

internal abstract class PendingTwoNodeModule(string tag) : PendingModule(tag)
{
    protected Node? CaptureNode;
    protected Node? PlaybackNode;
    internal IntPtr ModuleHandle = IntPtr.Zero;

    protected bool CanBeCompleted => CaptureNode is not null &&
                                     PlaybackNode is not null;

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
}