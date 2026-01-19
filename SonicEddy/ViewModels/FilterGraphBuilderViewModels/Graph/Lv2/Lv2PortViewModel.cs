using Fr.Lv2.Model;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Lv2;

public class Lv2PortViewModel(
    PortDescription description,
    NodeViewModelBase nodeViewModel)
    : PortViewModelBase(description.Name, nodeViewModel)
{
    public PortDescription Description { get; } = description;
}