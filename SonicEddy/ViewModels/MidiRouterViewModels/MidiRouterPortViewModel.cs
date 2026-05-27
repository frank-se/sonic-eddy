using Fr.Sonic.Model.Objects;
using SonicEddy.Controls.GraphEditorControl;

namespace SonicEddy.ViewModels.MidiRouterViewModels;

public sealed class MidiRouterPortViewModel(Port port, string name)
    : GraphPort(name)
{
    public Port Port { get; } = port;
}
