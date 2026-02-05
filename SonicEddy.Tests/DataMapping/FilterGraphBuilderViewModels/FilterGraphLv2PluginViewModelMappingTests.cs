using Fr.Lv2.Model;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Lv2;

namespace SonicEddy.Tests.DataMapping.FilterGraphBuilderViewModels;

[Collection("Lv2Collection")]
public class FilterGraphLv2PluginViewModelMappingTests
{
    [Fact]
    public void FromLv2PluginNodeViewModelMapping()
    {
        var pluginDescription = Fr.Lv2.Lv2.PluginDescriptions().Find(x =>
            x.Uri == "http://calf.sourceforge.net/plugins/Compressor");

        Assert.NotNull(pluginDescription);

        var pluginNode = new Lv2PluginNodeViewModel(pluginDescription!);

        var id = Guid.NewGuid();
        var result = pluginNode.ToGrpc(id);

        Assert.Equal("My Node", result.Name);
        Assert.Equal(id, result.Id);
        Assert.Equal(pluginDescription.Uri, result.Uri);

        // Input ports
        Assert.Equal(2, result.InputPorts.Count);
        var firstInPort = result.InputPorts.First();
        var secondInPort = result.InputPorts.Skip(1).First();

        var expectedInPorts = pluginDescription.Ports.Where(p =>
            p.Classes.Contains(Lv2PortUris.AudioPortUri) &&
            p.Classes.Contains(Lv2PortUris.InputPortUri)).ToList();

        var expectedFirstInPort = expectedInPorts.First();
        Assert.Equal(expectedFirstInPort.Name, firstInPort.Name);
        Assert.Equal(expectedFirstInPort.Symbol, firstInPort.Symbol);

        var expectedSecondInPort = expectedInPorts.Skip(1).First();
        Assert.Equal(expectedSecondInPort.Name, secondInPort.Name);
        Assert.Equal(expectedSecondInPort.Symbol, secondInPort.Symbol);

        // Output ports
        Assert.Equal(2, result.OutputPorts.Count);
        var firstOutPort = result.OutputPorts.First();
        var secondOutPort = result.OutputPorts.Skip(1).First();

        var expectedOutPorts = pluginDescription.Ports.Where(p =>
            p.Classes.Contains(Lv2PortUris.AudioPortUri) &&
            p.Classes.Contains(Lv2PortUris.OutputPortUri)).ToList();

        var expectedFirstOutPort = expectedOutPorts.First();
        Assert.Equal(expectedFirstOutPort.Name, firstOutPort.Name);
        Assert.Equal(expectedFirstOutPort.Symbol, firstOutPort.Symbol);

        var expectedSecondOutPort = expectedOutPorts.Skip(1).First();
        Assert.Equal(expectedSecondOutPort.Name, secondOutPort.Name);
        Assert.Equal(expectedSecondOutPort.Symbol, secondOutPort.Symbol);
    }
}