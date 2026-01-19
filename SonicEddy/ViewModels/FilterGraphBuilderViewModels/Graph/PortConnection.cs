namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph;

public record PortConnection(
    PortViewModelBase OutPort,
    PortViewModelBase InPort);