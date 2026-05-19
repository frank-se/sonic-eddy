using Fr.Sonic.Model.Config.FilterChain;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Conversions;

namespace SonicEddy.Tests.DataMapping.Conversions;

public class FilterGraphConfigTests
{
    [Fact]
    public void FilterGraphConfigFromFilterGraphGrpc()
    {
        List<Guid> outputNodePortIds = [Guid.NewGuid(), Guid.NewGuid()];
        List<Guid> inputNodePortIds = [Guid.NewGuid(), Guid.NewGuid()];
        List<Guid> pluginOneInputPorts = [Guid.NewGuid(), Guid.NewGuid()];
        List<Guid> pluginOneOutputPorts = [Guid.NewGuid(), Guid.NewGuid()];
        List<Guid> pluginTwoInputPorts = [Guid.NewGuid(), Guid.NewGuid()];
        List<Guid> pluginTwoOutputPorts = [Guid.NewGuid(), Guid.NewGuid()];

        var filterGraph = new FilterGraph(
            Guid.NewGuid(),
            "My Filter Graph",
            [
                new FilterGraphOutput(Guid.NewGuid(), [
                    new(outputNodePortIds[0], "FL"),
                    new(outputNodePortIds[1], "FR"),
                ]),
                new FilterGraphLv2Plugin(Guid.NewGuid(), "Compressor_1",
                    "http://calf.sourceforge.net/plugins/Compressor",
                    [
                        new(pluginOneInputPorts[0], "Input", "in_1"),
                        new(pluginOneInputPorts[1], "Input", "in_2"),
                    ],
                    [
                        new(pluginOneOutputPorts[0], "Output", "out_1"),
                        new(pluginOneOutputPorts[1], "Output", "out_2")
                    ]),
                new FilterGraphLv2Plugin(Guid.NewGuid(), "Compressor_2",
                    "http://calf.sourceforge.net/plugins/Compressor",
                    [
                        new(pluginTwoInputPorts[0], "Input", "in_1"),
                        new(pluginTwoInputPorts[1], "Input", "in_2"),
                    ],
                    [
                        new(pluginTwoOutputPorts[0], "Output", "out_1"),
                        new(pluginTwoOutputPorts[1], "Output", "out_2")
                    ]),
                new FilterGraphInput(Guid.NewGuid(), [
                    new(inputNodePortIds[0], "FL"),
                    new(inputNodePortIds[1], "FR")
                ])
            ],
            [
                new(inputNodePortIds[0], pluginOneInputPorts[0]),
                new(inputNodePortIds[1], pluginOneInputPorts[1]),
                new(pluginOneOutputPorts[0], pluginTwoInputPorts[0]),
                new(pluginOneOutputPorts[1], pluginTwoInputPorts[1]),
                new(pluginTwoOutputPorts[0], outputNodePortIds[0]),
                new(pluginTwoOutputPorts[1], outputNodePortIds[1])
            ], null);

        var result = filterGraph.ToFilterGraphConfig();

        Assert.NotNull(result.Inputs);
        Assert.Equal(2, result.Inputs.Count);
        Assert.Equal("Compressor_1:in_1", result.Inputs[0]);
        Assert.Equal("Compressor_1:in_2", result.Inputs[1]);

        Assert.NotNull(result.Outputs);
        Assert.Equal(2, result.Outputs.Count);
        Assert.Equal("Compressor_2:out_1", result.Outputs[0]);
        Assert.Equal("Compressor_2:out_2", result.Outputs[1]);

        Assert.Equal(2, result.Links.Count);

        var l1 = result.Links[0];
        Assert.Equal("Compressor_1:out_1", l1.Output);
        Assert.Equal("Compressor_2:in_1", l1.Input);

        var l2 = result.Links[1];
        Assert.Equal("Compressor_1:out_2", l2.Output);
        Assert.Equal("Compressor_2:in_2", l2.Input);
        
        Assert.Equal(2, result.Nodes.Count);
        
        var n1 = result.Nodes[0];
        Assert.Equal("Compressor_1", n1.Name);
        Assert.Equal("lv2", n1.Type);
        Assert.Equal("http://calf.sourceforge.net/plugins/Compressor", n1.Plugin);
        
        var n2 = result.Nodes[1];
        Assert.Equal("Compressor_2", n2.Name);
        Assert.Equal("lv2", n2.Type);
        Assert.Equal("http://calf.sourceforge.net/plugins/Compressor", n2.Plugin);
    }
}