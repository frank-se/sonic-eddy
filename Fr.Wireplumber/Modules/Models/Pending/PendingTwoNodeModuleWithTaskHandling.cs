using Fr.Wireplumber.Registries.Modules;

namespace Fr.Wireplumber.Modules.Models.Pending;

internal class PendingTwoNodeModuleWithTaskHandling<T>(
    string tag,
    Func<string, IntPtr, ulong, ulong, T> creator,
    ModuleRegistry moduleRegistry)
    : PendingTwoNodeModule(tag) where T : TwoNodePipewireModule
{
    private TaskCompletionSource<T> TaskCompletionSource { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal override bool TryComplete()
    {
        if (TaskCompletionSource.Task.IsCompleted) return false;

        using (Sync.EnterScope())
        {
            if (TaskCompletionSource.Task.IsCompleted) return false;
            if (!CanBeCompleted) return false;

            var graph = creator(Tag, ModuleHandle, CaptureNode!.ObjectSerial,
                PlaybackNode!.ObjectSerial);

            moduleRegistry.AddModule(graph);

            TaskCompletionSource.SetResult(graph);
            return true;
        }
    }

    internal Task<T> GetTask()
    {
        using (Sync.EnterScope())
        {
            return TaskCompletionSource.Task;
        }
    }

    internal void FinishWithException(Exception exception)
    {
        using (Sync.EnterScope())
        {
            TaskCompletionSource.TrySetException(exception);
        }
    }
}