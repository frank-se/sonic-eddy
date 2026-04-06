using System.Collections.Generic;
using System.Collections.ObjectModel;
using DynamicData;
using SonicEddy.Contracts.FilterGraph;

namespace SonicEddy.ViewModels.FilterGraphManagerViewModels;

public class FilterGraphPluginViewModel(
    List<FilterGraphParameterDescriptionViewModel> parameters,
    string name,
    FilterGraphLv2Plugin node)
    : ViewModelBase
{
    public List<FilterGraphParameterDescriptionViewModel> Parameters { get; } =
        parameters;

    public string Name { get; } = name;
    
    public FilterGraphLv2Plugin Node { get; } = node;
}