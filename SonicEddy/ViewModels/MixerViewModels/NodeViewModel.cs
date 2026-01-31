using Fr.Wireplumber.Model.Objects;
using ReactiveUI;

namespace SonicEddy.ViewModels.MixerViewModels;

public class NodeViewModel : ReactiveObject
{
    public required string Description { get; init; }
    public required Node Node { get; init; }
}