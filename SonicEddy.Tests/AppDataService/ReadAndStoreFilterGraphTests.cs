using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Services.AppData;

namespace SonicEddy.Tests.AppDataService;

public class ReadAndStoreFilterGraphTests
{
    private static readonly string TestFileFolder = Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
        "SonicEddyTests/FilterChains");

    private readonly IAppDataService _appDataService;

    public ReadAndStoreFilterGraphTests()
    {
        Directory.CreateDirectory(TestFileFolder);
        _appDataService = new Services.AppData.AppDataService(TestFileFolder, TestFileFolder, "");
    }

    [Fact]
    public async Task StoreAndReadFilterGraph()
    {
        var id = Guid.NewGuid();
        var lv2NodeId = Guid.NewGuid();
        var lv2InPortId = Guid.NewGuid();
        var lv2OutPortId = Guid.NewGuid();
        const string lv2InPortName = "In Port Name";
        const string lv2InPortSymbol = "In Port Symbol";
        const string lv2OutPortName = "Out Port Name";
        const string lv2OutPortSymbol = "Out Port Symbol";
        var filterGraph = new FilterGraph(
            id, "My Filter Graph with Nodes and Edges", [
                new FilterGraphLv2Plugin(lv2NodeId, "Lv2 Plugin Name", "Uri", [
                    new(lv2InPortId, lv2InPortName, lv2InPortSymbol)
                ], [
                    new(lv2OutPortId, lv2OutPortName, lv2OutPortSymbol)
                ])
            ], [
                new(lv2OutPortId, lv2InPortId)
            ]);

        await _appDataService.CreateFilterGraph(filterGraph);

        var testing = await _appDataService.GetFilterGraph(id);

        Assert.Equal(filterGraph.Id, testing.Id);
        Assert.Equal(filterGraph.Name, testing.Name);
        Assert.Equal(filterGraph.Nodes.Count, testing.Nodes.Count);
        Assert.Equal(lv2NodeId, testing.Nodes.First().Id);

        Assert.IsType<FilterGraphLv2Plugin>(testing.Nodes.First());
        var plugin = testing.Nodes.First() as FilterGraphLv2Plugin;
        Assert.Equal("Uri", plugin!.Uri);
        Assert.Equal("Lv2 Plugin Name", plugin.Name);

        var inputPort = plugin.InputPorts.First();
        Assert.Equal(lv2InPortId, inputPort.Id);
        Assert.Equal(lv2InPortName, inputPort.Name);
        Assert.Equal(lv2InPortSymbol, inputPort.Symbol);

        var outputPort = plugin.OutputPorts.First();
        Assert.Equal(lv2OutPortId, outputPort.Id);
        Assert.Equal(lv2OutPortName, outputPort.Name);
        Assert.Equal(lv2OutPortSymbol, outputPort.Symbol);

        Assert.Single(testing.Edges);
        Assert.Equal(lv2OutPortId, testing.Edges.First().Source);
        Assert.Equal(lv2InPortId, testing.Edges.First().Target);
    }

    [Fact]
    public async Task ReadAllFilterGraphs()
    {
        var id = Guid.NewGuid();
        var lv2NodeId = Guid.NewGuid();
        var lv2InPortId = Guid.NewGuid();
        var lv2OutPortId = Guid.NewGuid();
        const string lv2InPortName = "In Port Name";
        const string lv2InPortSymbol = "In Port Symbol";
        const string lv2OutPortName = "Out Port Name";
        const string lv2OutPortSymbol = "Out Port Symbol";
        var filterGraph = new FilterGraph(
            id, "My Filter Graph with Nodes and Edges", [
                new FilterGraphLv2Plugin(lv2NodeId, "Lv2 Plugin Name", "Uri", [
                    new(lv2InPortId, lv2InPortName, lv2InPortSymbol)
                ], [
                    new(lv2OutPortId, lv2OutPortName, lv2OutPortSymbol)
                ])
            ], [
                new(lv2OutPortId, lv2InPortId)
            ]);

        await _appDataService.CreateFilterGraph(filterGraph);

        var filterGraphs = await _appDataService.GetAllFilterGraphs();
        Assert.Contains(filterGraphs, f => f.Id == id);
    }
}