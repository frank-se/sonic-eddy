using Fr.Sonic.Model.Objects;
using SonicEddy.Controls.GraphEditorControl;

namespace SonicEddy.ViewModels.MidiRouterViewModels;

public sealed class MidiRouterEdgeViewModel(
    string name,
    GraphPort source,
    GraphPort target,
    Link link) : GraphEdge(name, source, target)
{
    public Link Link { get; } = link;
}
