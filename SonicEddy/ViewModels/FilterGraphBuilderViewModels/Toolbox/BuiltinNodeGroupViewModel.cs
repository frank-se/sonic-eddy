using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SonicEddy.Contracts.FilterGraph;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Toolbox;

public class BuiltinNodeGroupViewModel(string name, IEnumerable<BuiltinNodeType> types)
{
    public string Name { get; } = name;
    public ObservableCollection<AvailableBuiltinNode> Nodes { get; } =
        new(types.Select(t => new AvailableBuiltinNode(t)));
}
