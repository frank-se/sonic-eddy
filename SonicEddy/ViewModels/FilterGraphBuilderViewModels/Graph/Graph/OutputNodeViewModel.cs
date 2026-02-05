namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Graph;

public class OutputNodeViewModel() : NodeViewModelBase("Outputs",
[
    new InputPortViewModel("FL"),
    new InputPortViewModel("FR")
], []);