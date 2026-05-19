using Fr.Sonic.Model.Objects;
using SonicEddy.Controls.GraphEditorControl;

namespace SonicEddy.ViewModels.MidiConnectionEditorViewModels.Graph;

public class MidiPortViewModel(Port port) : GraphPort(port.Alias!)
{
    public Port Port { get; } = port;
}