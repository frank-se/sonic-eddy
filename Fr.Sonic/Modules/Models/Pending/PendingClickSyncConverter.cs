using Fr.Sonic.Model.Objects;
using Fr.Sonic.Registries.Modules;

namespace Fr.Sonic.Modules.Models.Pending;

internal sealed class PendingClickSyncConverter(
    string tag,
    Func<string, IntPtr, ulong, ulong, ulong, ClickSyncConverter> creator,
    ModuleRegistry moduleRegistry)
    : PendingModule(tag)
{
    private readonly TaskCompletionSource<ClickSyncConverter> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Node? _clickNode;
    private Node? _resetNode;
    private Node? _runNode;
    private IntPtr _moduleHandle;
    private bool _moduleHandleReady;

    internal void SetClickNode(Node node)
    {
        using (Sync.EnterScope())
            _clickNode = node;
    }

    internal void SetResetNode(Node node)
    {
        using (Sync.EnterScope())
            _resetNode = node;
    }

    internal void SetRunNode(Node node)
    {
        using (Sync.EnterScope())
            _runNode = node;
    }

    internal void SetModuleHandle(IntPtr moduleHandle)
    {
        using (Sync.EnterScope())
        {
            _moduleHandle = moduleHandle;
            _moduleHandleReady = true;
        }
    }

    internal override bool TryComplete()
    {
        if (_completion.Task.IsCompleted) return false;

        using (Sync.EnterScope())
        {
            if (_completion.Task.IsCompleted || !_moduleHandleReady ||
                _clickNode is null || _resetNode is null || _runNode is null)
                return false;

            var module = creator(Tag, _moduleHandle,
                _clickNode.ObjectSerial, _resetNode.ObjectSerial,
                _runNode.ObjectSerial);
            moduleRegistry.AddModule(module);
            _completion.SetResult(module);
            return true;
        }
    }

    internal Task<ClickSyncConverter> GetTask()
    {
        using (Sync.EnterScope())
            return _completion.Task;
    }

    internal void FinishWithException(Exception exception)
    {
        using (Sync.EnterScope())
            _completion.TrySetException(exception);
    }
}
