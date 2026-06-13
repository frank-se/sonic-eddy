using Fr.Sonic.Registries.Modules;

namespace Fr.Sonic.Modules.Models.Pending;

internal class PendingThreeNodeModuleWithTaskHandling<T>(
    string tag,
    Func<string, IntPtr, ulong, ulong, ulong, T> creator,
    ModuleRegistry moduleRegistry)
    : PendingThreeNodeModule(tag) where T : ThreeNodePipewireModule
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

            var module = creator(Tag, ModuleHandle,
                FirstNode!.ObjectSerial, SecondNode!.ObjectSerial,
                ThirdNode!.ObjectSerial);

            moduleRegistry.AddModule(module);
            TaskCompletionSource.SetResult(module);
            return true;
        }
    }

    internal Task<T> GetTask()
    {
        using (Sync.EnterScope())
            return TaskCompletionSource.Task;
    }

    internal void FinishWithException(Exception exception)
    {
        using (Sync.EnterScope())
            TaskCompletionSource.TrySetException(exception);
    }
}
