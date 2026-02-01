using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ProtoBuf;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Contracts.Mixers;

namespace SonicEddy.Services.AppData;

public class AppDataService(
    string filterGraphFolderPath,
    string mixerFolderPath) : IAppDataService
{
    private readonly AppDataServiceBase<FilterGraph>
        _filterGraphAppDataService =
            new(filterGraphFolderPath, "fc");

    private readonly AppDataServiceBase<Mixer>
        _mixerAppDataService = new(mixerFolderPath, "mix");

    public Task<FilterGraph> GetFilterGraph(Guid id) =>
        _filterGraphAppDataService.Get(id);

    public Task CreateFilterGraph(FilterGraph filterGraph) =>
        _filterGraphAppDataService.Create(filterGraph.Id, filterGraph);

    public void DeleteFilterGraph(Guid id) =>
        _filterGraphAppDataService.Delete(id);

    public Task<List<FilterGraph>> GetAllFilterGraphs() =>
        _filterGraphAppDataService.GetAll();

    public Task<List<Mixer>> GetAllMixers() => _mixerAppDataService.GetAll();

    public Task<Mixer> GetMixer(Guid id) => _mixerAppDataService.Get(id);

    public Task CreateMixer(Mixer mixer) =>
        _mixerAppDataService.Create(mixer.Id, mixer);

    public void DeleteMixer(Guid id) => _mixerAppDataService.Delete(id);
}