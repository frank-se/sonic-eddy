using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ProtoBuf;

namespace SonicEddy.Services.AppData;

public class AppDataServiceBase<T>(string folderPath, string filePrefix)
{
    private string BuildFileName(Guid id) =>
        Path.Combine(folderPath, $"{filePrefix}-{id}.grpc");

    private readonly string _searchPattern = $"{filePrefix}-*.grpc";

    public async Task<T> Get(Guid id)
    {
        var fileName = BuildFileName(id);
        var bytes = await File.ReadAllBytesAsync(fileName);
        using var memoryStream = new MemoryStream(bytes);
        return Serializer.Deserialize<T>(memoryStream);
    }

    public async Task Create(Guid id, T data)
    {
        var fileName = BuildFileName(id);
        var file = File.Create(fileName);
        Serializer.Serialize(file, data);
        await file.FlushAsync();
        file.Close();
    }

    public void Delete(Guid id)
    {
        var fileName = BuildFileName(id);
        File.Delete(fileName);
    }

    public async Task<List<T>> GetAll()
    {
        var results = new List<T>();
        var files = Directory.EnumerateFiles(folderPath, _searchPattern);

        foreach (var fileName in files)
        {
            var bytes = await File.ReadAllBytesAsync(fileName);
            using var memoryStream = new MemoryStream(bytes);
            results.Add(Serializer.Deserialize<T>(memoryStream));
        }

        return results;
    }
}