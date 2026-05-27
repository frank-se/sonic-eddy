using Fr.Sonic.Model.Objects;

namespace SonicEddy.ViewModels.MidiRouterViewModels;

public sealed class MidiRouterPortOptionViewModel(Port port, string name)
{
    public Port Port { get; } = port;
    public string Name { get; } = name;
}
