using SonicEddy.Contracts.FilterGraph;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Graph;

namespace SonicEddy.Tests.DataMapping.FilterGraphBuilderViewModels;

public class FilterGraphOutputNodeViewModelMappingTests
{
    [Fact]
    public void FromOutputNodeViewModel()
    {
        var outputNode = new OutputNodeViewModel();

        var id = Guid.NewGuid();

        var result = outputNode.ToGrpc(id);
        Assert.Equal("Output", result.Name);
        Assert.Equal(id, result.Id);
        
        Assert.Equal(2, result.InputPorts.Count);
        
        var firstInPort = result.InputPorts.First();
        Assert.IsType<FilterGraphOutputInputPort>(firstInPort);
        
        var secondInPort = result.InputPorts.Skip(1).First();
        Assert.IsType<FilterGraphOutputInputPort>(secondInPort);
        
        Assert.Equal("FL", firstInPort.Name);
        Assert.Equal("FR", secondInPort.Name);
    }
}