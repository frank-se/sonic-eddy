namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Graph;

public class InputNodeViewModel() : NodeViewModelBase("Inputs", [], [
    new InputPortViewModel("FL"), new InputPortViewModel("FR")
]);