using System.Collections.Generic;
using ReactiveUI;

namespace SonicEddy.ViewModels.MixerViewModels;

public class PluginViewModel : ReactiveObject
{
    public required string Name { get; init; }
    public required List<ParameterViewModel> Parameters { get; init; }
}