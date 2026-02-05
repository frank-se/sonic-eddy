using System.Collections.ObjectModel;
using SonicEddy.Controls.GraphEditorControl;

namespace SonicEddy.ViewModels.MidiConnectionEditorViewModels.Graph;

public class MidiInputPortsViewModel(
    string name,
    ReadOnlyCollection<GraphPort> outPorts) : GraphNode(name, [], outPorts)
{
}