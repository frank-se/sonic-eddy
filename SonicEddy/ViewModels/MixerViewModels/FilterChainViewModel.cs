using System.Collections.Generic;
using ReactiveUI;

namespace SonicEddy.ViewModels.MixerViewModels;

public class FilterChainViewModel : ReactiveObject
{
    public required List<ParameterViewModel> Parameters { get; init; }
}