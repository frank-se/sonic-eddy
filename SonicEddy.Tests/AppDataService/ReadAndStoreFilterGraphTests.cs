using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Services.AppData;

namespace SonicEddy.Tests.AppDataService;

public class ReadAndStoreFilterGraphTests
{
    private static readonly string TestFileFolder = Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "SonicEddyTests/FilterChains");

    private readonly IAppDataService _appDataService;

    public ReadAndStoreFilterGraphTests()
    {
        Directory.CreateDirectory(TestFileFolder);
        _appDataService = new Services.AppData.AppDataService(TestFileFolder);
    }

    [Fact]
    public async Task StoreAndReadFilterGraph()
    {
        var id = Guid.NewGuid();
        var lv2NodeId = Guid.NewGuid();
        var lv2InPortId = Guid.NewGuid();
        var lv2OutPortId = Guid.NewGuid();
        const string lv2InPortName = "In Port Name";
        const string lv2OutPortName = "Out Port Name";
        var filterGraph = new FilterGraph(
            id, "My Filter Graph with Nodes and Edges", [
                new FilterGraphLv2Plugin(lv2NodeId, "Lv2 Plugin Name", [
                    new(lv2InPortId, lv2InPortName)
                ], [
                    new(lv2OutPortId, lv2OutPortName)
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
        const string lv2OutPortName = "Out Port Name";
        var filterGraph = new FilterGraph(
            id, "My Filter Graph with Nodes and Edges", [
                new FilterGraphLv2Plugin(lv2NodeId, "Lv2 Plugin Name", [
                    new(lv2InPortId, lv2InPortName)
                ], [
                    new(lv2OutPortId, lv2OutPortName)
                ])
            ], [
                new(lv2OutPortId, lv2InPortId)
            ]);

        await _appDataService.CreateFilterGraph(filterGraph);

        var filterGraphs = await _appDataService.GetAllFilterGraphs();
        Assert.Contains(filterGraphs, f => f.Id == id);
    }
}