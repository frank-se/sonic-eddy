using Fr.Wireplumber.Modules.Models;

namespace SonicEddy.ViewModels.ModuleDetailsViewModels;

public abstract class ModuleDetailsViewModelBase(TwoNodePipewireModule module)
    : ViewModelBase
{
    public string Name { get; } = module.Name;
    public string CaptureModuleName { get; } = module.CaptureNode.Name!;
    public string PlaybackModuleName { get; } = module.PlaybackNode.Name!;
}