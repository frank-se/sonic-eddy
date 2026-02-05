using Fr.Wireplumber.Model.Objects;
using SonicEddy.Controls.GraphEditorControl;

namespace SonicEddy.ViewModels.MidiConnectionEditorViewModels.Graph;

public class MidiPortViewModel(Port port) : GraphPort(port.Alias!)
{
}