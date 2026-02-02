using ReactiveUI.Avalonia;
using SonicEddy.ViewModels.GraphControlTesterViewModels;

namespace SonicEddy.Views.GraphControlTesterViews;

public partial class
    GraphControlTesterView : ReactiveUserControl<GraphControlTesterViewModel>
{
    public GraphControlTesterView()
    {
        InitializeComponent();
    }
}