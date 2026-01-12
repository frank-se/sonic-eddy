namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels;

public record PortConnection(
    PortNodeBase OutPort,
    PortNodeBase InPort);