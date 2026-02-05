using System.Collections.ObjectModel;
using SonicEddy.Controls.GraphEditorControl;

namespace SonicEddy.ViewModels.MidiConnectionEditorViewModels.Graph;

public class MidiOutputPortsViewModel(
    string name,
    ReadOnlyCollection<GraphPort> inPorts) : GraphNode(name, inPorts, [])
{
}