using System.Collections.Generic;

namespace SonicEddy.ViewModels.ModuleManagerViewModels;

public class NodeViewModel : ViewModelBase
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public ulong ObjectSerial { get; init; }

    public required List<PropertyInfoViewModel> PropertyInfos { get; init; }
}