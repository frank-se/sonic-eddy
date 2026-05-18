using System.Collections.Concurrent;
using Fr.Sonic.Modules.Models;

namespace Fr.Sonic.Registries.Modules;

/// <summary>
/// Provides access to modules created by the module factory
/// </summary>
public class ModuleRegistry
{
    private readonly ConcurrentDictionary<string, PipewireModule>
        _modules = new();

    /// <summary>
    /// Give read-only access to the modules in the registry
    /// </summary>
    public IReadOnlyCollection<PipewireModule> Modules =>
        (IReadOnlyCollection<PipewireModule>)_modules.Values;

    /// <summary>
    /// Event is triggered when a module is added to the registry
    /// </summary>
    public event Action<PipewireModule>? Added;

    /// <summary>
    /// Event is triggered when a module is deleted from the registry
    /// </summary>
    public event Action<PipewireModule>? Deleted;

    internal void AddModule(PipewireModule module)
    {
        if (!_modules.TryAdd(module.Tag, module)) return;
        module.ModuleRegistry = this;
        Added?.Invoke(module);
    }

    internal void DeleteModule(string tag)
    {
        if (_modules.TryRemove(tag, out var module))
        {
            Deleted?.Invoke(module);
        }
    }
}