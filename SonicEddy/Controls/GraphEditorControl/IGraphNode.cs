using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace SonicEddy.Controls.GraphEditorControl;

public interface IGraphNode
{
    ReadOnlyCollection<IGraphPort> InPorts { get; }
    ReadOnlyCollection<IGraphPort> OutPorts { get; }
    string Name { get; }
    Control? Control { get; set; }
}