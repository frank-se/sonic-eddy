using System.Collections.ObjectModel;
using ReactiveUI;
using SonicEddy.Controls.GraphEditorControl;

namespace SonicEddy.ViewModels.GraphControlTesterViewModels;

public class GraphControlTesterViewModel : ViewModelBase,
    IActivatableViewModel, IRoutableViewModel
{
    public ObservableCollection<IGraphNode> Nodes { get; }

    public GraphControlTesterViewModel(string? urlPathSegment,
        IScreen hostScreen)
    {
        UrlPathSegment = urlPathSegment;
        HostScreen = hostScreen;

        Nodes =
        [
            new TestNodeViewModel(
                [
                    new TestPortViewModel("in_1"),
                    new TestPortViewModel("in_2"),
                ],
                [
                    new TestPortViewModel("out_1"),
                    new TestPortViewModel("out_2")
                ], "Name", null),
            new TestNodeViewModel(
                [
                    new TestPortViewModel("in_1"),
                    new TestPortViewModel("in_2"),
                ],
                [
                    new TestPortViewModel("out_1"),
                    new TestPortViewModel("out_2")
                ], "Name 2", null),
        ];
    }

    public ViewModelActivator Activator { get; } = new();
    public string? UrlPathSegment { get; }
    public IScreen HostScreen { get; }
}