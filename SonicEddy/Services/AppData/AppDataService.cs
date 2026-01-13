using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ProtoBuf;
using SonicEddy.Contracts.FilterGraph;

namespace SonicEddy.Services.AppData;

public class AppDataService(string filterGraphFolderPath) : IAppDataService
{
    private string BuildFilename(Guid id) => Path.Combine(filterGraphFolderPath,
        $"fc-{id}.grpc");

    public async Task<FilterGraph> GetFilterGraph(Guid id)
    {
        var fileName = BuildFilename(id);
        var bytes = await File.ReadAllBytesAsync(fileName);
        using var memoryStream = new MemoryStream(bytes);
        return Serializer.Deserialize<FilterGraph>(memoryStream);
    }

    public async Task CreateFilterGraph(FilterGraph filterGraph)
    {
        var fileName = BuildFilename(filterGraph.Id);
        var file = File.Create(fileName);
        Serializer.Serialize(file, filterGraph);
        await file.FlushAsync();
        file.Close();
    }

    public async Task<List<FilterGraph>> GetAllFilterGraphs()
    {
        var results = new List<FilterGraph>();
        var files =
            Directory.EnumerateFiles(filterGraphFolderPath, "fc-*.grpc");
        foreach (var fileName in files)
        {
            var bytes = await File.ReadAllBytesAsync(fileName);
            using var memoryStream = new MemoryStream(bytes);
            results.Add(Serializer.Deserialize<FilterGraph>(memoryStream));
        }

        return results;
    }
}