using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;

namespace SonicEddy.Controls.GraphEditorControl;

public class GraphNode(
    string name,
    ReadOnlyCollection<GraphPort> inPorts,
    ReadOnlyCollection<GraphPort> outPorts) : GraphElementBase(name)
{
    public ReadOnlyCollection<GraphPort> InPorts { get; } = inPorts;
    public ReadOnlyCollection<GraphPort> OutPorts { get; } = outPorts;
}