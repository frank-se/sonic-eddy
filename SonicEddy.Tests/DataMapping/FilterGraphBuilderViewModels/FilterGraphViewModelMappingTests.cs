using Fr.Lv2.Model;
using NSubstitute;
using ReactiveUI;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Services.AppData;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Lv2;

namespace SonicEddy.Tests.DataMapping.FilterGraphBuilderViewModels;

[Collection("Lv2Collection")]
public class FilterGraphViewModelMappingTests
{
    [Fact]
    public void FromFilterGraphBuilderViewModel()
    {
        // Setup
        var pluginDescription = Fr.Lv2.Lv2.PluginDescriptions().Find(x =>
            x.Uri == "http://calf.sourceforge.net/plugins/Compressor");

        Assert.NotNull(pluginDescription);

        var appDataService = Substitute.For<IAppDataService>();
        var screen = Substitute.For<IScreen>();

        var viewModel =
            new FilterGraphBuilderViewModel(appDataService, string.Empty,
                screen, Task.FromResult<List<ClassDescription>>([]),
                Task.FromResult<List<PluginDescription>>([]))
            {
                Name = "My Filter Graph",
                Id = Guid.NewGuid()
            };

        viewModel.Nodes.Add(new Lv2PluginNodeViewModel(pluginDescription)
        {
            Name = "My Test Plugin"
        });

        viewModel.Nodes.Add(new Lv2PluginNodeViewModel(pluginDescription)
        {
            Name = "My Second Test Plugin"
        });

        var sourceInputNode = viewModel.Nodes[0];
        var sourceOutputNode = viewModel.Nodes[1];
        var sourceFirstNode = viewModel.Nodes[2] as Lv2PluginNodeViewModel;
        var sourceSecondNode = viewModel.Nodes[3] as Lv2PluginNodeViewModel;

        viewModel.Connections.Add(new(sourceInputNode.OutPorts[0],
            sourceFirstNode!.InPorts[0]));
        viewModel.Connections.Add(new(sourceInputNode.OutPorts[1],
            sourceFirstNode.InPorts[1]));

        viewModel.Connections.Add(new(sourceFirstNode.OutPorts[0],
            sourceSecondNode!.InPorts[0]));
        viewModel.Connections.Add(new(sourceFirstNode.OutPorts[1],
            sourceSecondNode.InPorts[1]));

        viewModel.Connections.Add(new(sourceSecondNode.OutPorts[0],
            sourceOutputNode.InPorts[0]));
        viewModel.Connections.Add(new(sourceSecondNode.OutPorts[1],
            sourceOutputNode.InPorts[1]));

        // Method under test
        var result = viewModel.ToGrpc();

        // Checks
        Assert.Equal("My Filter Graph", result.Name);
        Assert.Equal(viewModel.Id, result.Id);

        Assert.Equal(4, result.Nodes.Count);

        var targetInputNode = result.Nodes[0];
        Assert.IsType<FilterGraphInput>(targetInputNode);
        Assert.Equal(sourceInputNode.Name, targetInputNode.Name);
        Assert.Equal(sourceInputNode.Id, targetInputNode.Id);

        var targetOutputNode = result.Nodes[1];
        Assert.IsType<FilterGraphOutput>(targetOutputNode);
        Assert.Equal(sourceOutputNode.Name, targetOutputNode.Name);
        Assert.Equal(sourceOutputNode.Id, targetOutputNode.Id);

        var targetFirstNode = result.Nodes[2];
        Assert.IsType<FilterGraphLv2Plugin>(targetFirstNode);
        Assert.Equal(sourceFirstNode.Name, targetFirstNode.Name);
        Assert.Equal(sourceFirstNode.Id, targetFirstNode.Id);
        var nodeLv2 = targetFirstNode as FilterGraphLv2Plugin;
        Assert.Equal(sourceFirstNode.Plugin.Uri, nodeLv2!.Uri);

        var targetSecondNode = result.Nodes[3];
        Assert.IsType<FilterGraphLv2Plugin>(targetSecondNode);
        Assert.Equal(sourceSecondNode.Name, targetSecondNode.Name);
        Assert.Equal(sourceSecondNode.Id, sourceSecondNode.Id);
        nodeLv2 = targetFirstNode as FilterGraphLv2Plugin;
        Assert.Equal(sourceSecondNode.Plugin.Uri, nodeLv2!.Uri);

        Assert.Equal(6, result.Edges.Count);

        var e1 = result.Edges[0];
        var n1 = targetInputNode as FilterGraphInput;
        var n2 = targetFirstNode as FilterGraphLv2Plugin;
        Assert.Equal(n1!.OutputPorts[0].Id, e1.Source);
        Assert.Equal(n2!.InputPorts[0].Id, e1.Target);

        var e2 = result.Edges[1];
        Assert.Equal(n1!.OutputPorts[1].Id, e2.Source);
        Assert.Equal(n2!.InputPorts[1].Id, e2.Target);

        var e3 = result.Edges[2];
        var n3 = targetSecondNode as FilterGraphLv2Plugin;
        Assert.Equal(n2.OutputPorts[0].Id, e3.Source);
        Assert.Equal(n3!.InputPorts[0].Id, e3.Target);

        var e4 = result.Edges[3];
        Assert.Equal(n2.OutputPorts[1].Id, e4.Source);
        Assert.Equal(n3.InputPorts[1].Id, e4.Target);

        var e5 = result.Edges[4];
        var n4 = targetOutputNode as FilterGraphOutput;
        Assert.Equal(n3.OutputPorts[0].Id, e5.Source);
        Assert.Equal(n4!.InputPorts[0].Id, e5.Target);

        var e6 = result.Edges[5];
        Assert.Equal(n3.OutputPorts[1].Id, e6.Source);
        Assert.Equal(n4.InputPorts[1].Id, e6.Target);
    }
}