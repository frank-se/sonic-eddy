namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Graph;

public class InputNodeViewModel : NodeViewModelBase
{
    public InputNodeViewModel() : base()
    {
        OutPorts =
        [
            new InputPortViewModel("FL", this), new InputPortViewModel("FR", this)
        ];
    }
}