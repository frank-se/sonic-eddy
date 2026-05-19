using Fr.Sonic.Model.Lv2;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Lv2;

public class Lv2PortViewModel(
    PortDescription description)
    : PortViewModelBase(description.Name)
{
    public PortDescription Description { get; } = description;
}