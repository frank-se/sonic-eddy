using System.Collections.ObjectModel;
using Avalonia.Controls;
using SonicEddy.Controls.GraphEditorControl;

namespace SonicEddy.ViewModels.GraphControlTesterViewModels;

public class TestNodeViewModel(
    ReadOnlyCollection<IGraphPort> inPorts,
    ReadOnlyCollection<IGraphPort> outPorts,
    string name,
    Control? control)
    : IGraphNode
{
    public ReadOnlyCollection<IGraphPort> InPorts { get; } = inPorts;
    public ReadOnlyCollection<IGraphPort> OutPorts { get; } = outPorts;
    public string Name { get; } = name;
    public Control? Control { get; set; } = control;
}