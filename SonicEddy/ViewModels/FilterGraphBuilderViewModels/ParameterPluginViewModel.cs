using System;
using System.Collections.ObjectModel;
using ReactiveUI;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels;

public class ParameterPluginViewModel : ReactiveObject
{
    public ParameterPluginViewModel(Guid nodeId, string nodeName)
    {
        NodeId = nodeId;
        NodeName = nodeName;
    }

    public Guid NodeId { get; }
    public string NodeName { get; }
    public ObservableCollection<ParameterItemViewModel> Parameters { get; } = [];
}
