namespace Fr.Sonic.Modules.Models.Pending;

internal abstract class PendingModule(string tag)
{
    protected readonly string Tag = tag;
    protected readonly Lock Sync = new();

    internal abstract bool TryComplete();
}