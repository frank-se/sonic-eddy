using Fr.Wireplumber.PInvoke;
using Fr.Wireplumber.Registries.Modules;

namespace Fr.Wireplumber.Modules.Models;

/// <summary>
/// A pipewire module.
/// </summary>
/// <param name="Name">
/// User-readable, internal name for the module
/// </param>
/// <param name="Tag">
/// The tag, the factory creates the tag when creating the modules. It
/// injects the tag into the config, and uses it to identify the nodes that
/// were created for the module.
/// </param>
/// <param name="ModuleHandle">
/// The pipewire handle used to identify the module.
/// </param>
public abstract record PipewireModule(
    string Name,
    string Tag,
    IntPtr ModuleHandle)
{
    internal ModuleRegistry? ModuleRegistry { private get; set; }

    /// <summary>
    /// Destroy the pipewire module.
    /// </summary>
    public void Destroy()
    {
        FrWireplumberLib.DestroyModule(ModuleHandle);
        ModuleRegistry?.DeleteModule(Tag);
    }
};