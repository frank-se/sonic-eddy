namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Graph;

public class OutputNodeViewModel : NodeViewModelBase
{
    public OutputNodeViewModel() : base()
    {
        InPorts =
        [
            new InputPortViewModel("FL", this),
            new InputPortViewModel("FR", this)
        ];
    }
}