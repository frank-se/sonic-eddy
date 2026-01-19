using System.Collections.Generic;

namespace SonicEddy.ViewModels.ModuleManagerViewModels;

public abstract class ModuleViewModelBase : ViewModelBase
{
    public required string Name { get; init; }
    public required NodeViewModel CaptureNode { get; init; }
    public required NodeViewModel PlaybackNode { get; init; }
}