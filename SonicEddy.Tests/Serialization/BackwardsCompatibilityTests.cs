using ProtoBuf;
using ProtoBuf.Meta;
using SonicEddy.Contracts.FilterGraph;

namespace SonicEddy.Tests.Serialization;

public class BackwardsCompatibilityTests
{
    [Fact]
    public async Task FilterGraphDeserialization()
    {
        /*
        var file = File.Create("/home/frank/empty_filter_graph.bin");
        Serializer.Serialize(file, new FilterGraph());
        await file.FlushAsync();
         */
        var bytes =
            await File.ReadAllBytesAsync("TestFiles/empty_filter_graph.bin");
        using var memoryStream = new MemoryStream(bytes);
        var filterGraph = Serializer.Deserialize<FilterGraph>(memoryStream);
        Assert.Equal(string.Empty, filterGraph.Name);
        Assert.Equal(Guid.Empty, filterGraph.Id);
        Assert.Equal([], filterGraph.Nodes);
        Assert.Equal([], filterGraph.Edges);
    }

    [Fact]
    public async Task FilterGraphWithIdAndNameSerialization()
    {
        var id = Guid.Parse("502c01fd-43de-4b28-8e78-d67019178b89");

        /*
        var graph = new FilterGraph(id, "My Filter Graph", [], []);
        using var file =
            File.Create("/home/frank/filter_graph_with_id_and_name.bin");
        Serializer.Serialize(
            file,
            graph);
        await file.FlushAsync();

        var outputStream = new MemoryStream();
        Serializer.Serialize(outputStream, graph);
        Assert.Equal(37, outputStream.Length);
        */

        var bytes =
            await File.ReadAllBytesAsync(
                "TestFiles/filter_graph_with_id_and_name.bin");
        using var memoryStream = new MemoryStream(bytes);
        var filterGraph = Serializer.Deserialize<FilterGraph>(memoryStream);
        Assert.Equal("My Filter Graph", filterGraph.Name);
        Assert.Equal(id, filterGraph.Id);
        Assert.Equal([], filterGraph.Nodes);
        Assert.Equal([], filterGraph.Edges);
    }

    [Fact]
    public async Task FilterGraphWithNodesAndEdges()
    {
        var id = Guid.Parse("b3c68f95-19cb-451b-ae88-0dabc67c31a6");
        var lv2NodeId = Guid.Parse("442988f5-3f80-4b80-96fe-42f06933f4d6");
        var lv2InPortId = Guid.Parse("623d1797-359a-44b9-8564-0248f7709aef");
        var lv2OutPortId = Guid.Parse("2d46556a-a765-4d79-aa53-02629bafebf6");
        const string lv2InPortName = "In Port Name";
        const string lv2OutPortName = "Out Port Name";
        /*
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
        using var file =
            File.Create("/home/frank/filter_graph_with_node_and_edge.bin");
        Serializer.Serialize(
            file,
            filterGraph);
        await file.FlushAsync();
        */
        
        var bytes =
            await File.ReadAllBytesAsync(
                "TestFiles/filter_graph_with_node_and_edge.bin");
        using var memoryStream = new MemoryStream(bytes);
        var filterGraph = Serializer.Deserialize<FilterGraph>(memoryStream);
        Assert.Equal(id, filterGraph.Id);
        Assert.Equal("My Filter Graph with Nodes and Edges", filterGraph.Name);
        Assert.Single(filterGraph.Nodes);
        Assert.Equal(lv2NodeId, filterGraph.Nodes.First().Id);
    }
}