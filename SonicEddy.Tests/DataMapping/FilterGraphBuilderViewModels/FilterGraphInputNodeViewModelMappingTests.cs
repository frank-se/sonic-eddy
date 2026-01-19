using SonicEddy.Contracts.FilterGraph;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Graph;

namespace SonicEddy.Tests.DataMapping.FilterGraphBuilderViewModels;

public class FilterGraphInputNodeViewModelMappingTests
{
    [Fact]
    public void FromInputNodeViewModel()
    {
        var inputNode = new InputNodeViewModel();

        var id = Guid.NewGuid();

        var result = inputNode.ToGrpc(id);
        Assert.Equal("Input", result.Name);
        Assert.Equal(id, result.Id);

        Assert.Equal(2, result.OutputPorts.Count);

        var firstOutputPort = result.OutputPorts.First();
        Assert.IsType<FilterGraphInputOutputPort>(firstOutputPort);

        var secondOutputPort = result.OutputPorts.Skip(1).First();
        Assert.IsType<FilterGraphInputOutputPort>(secondOutputPort);

        Assert.Equal("FL", firstOutputPort.Name);
        Assert.Equal("FR", secondOutputPort.Name);
    }
}