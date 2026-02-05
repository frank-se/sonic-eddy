using System.Collections.ObjectModel;
using ReactiveUI;
using SonicEddy.Controls.GraphEditorControl;

namespace SonicEddy.ViewModels.GraphControlTesterViewModels;

public class GraphControlTesterViewModel : ViewModelBase,
    IActivatableViewModel, IRoutableViewModel
{
    public GraphNode Inputs { get; } = new GraphNode(
        "Inputs",
        [],
        [
            new("in_1"),
            new("in_2"),
            new("in_3"),
            new("in_4"),
            new("in_5"),
        ]);

    public GraphNode Outputs { get; } = new GraphNode(
        "Outputs",
        [
            new("out_1"),
            new("out_2"),
            new("out_3"),
            new("out_4"),
            new("out_5"),
            new("out_6"),
        ],
        []);

    public ObservableCollection<GraphNode> Nodes { get; }

    public GraphControlTesterViewModel(string? urlPathSegment,
        IScreen hostScreen)
    {
        UrlPathSegment = urlPathSegment;
        HostScreen = hostScreen;

        Nodes =
        [
            new GraphNode(
                "Node 1",
                [
                    new GraphPort("in_1"),
                    new GraphPort("in_2"),
                ],
                [
                    new GraphPort("out_1"),
                    new GraphPort("out_2")
                ]),
            new GraphNode(
                "Node 1",
                [
                    new GraphPort("in_1"),
                    new GraphPort("in_2"),
                ],
                [
                    new GraphPort("out_1"),
                    new GraphPort("out_2")
                ]),
        ];
    }

    public ViewModelActivator Activator { get; } = new();
    public string? UrlPathSegment { get; }
    public IScreen HostScreen { get; }
}