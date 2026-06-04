using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ProtoBuf;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Contracts.MidiSync;
using SonicEddy.Contracts.Mixers;

namespace SonicEddy.Services.AppData;

public class AppDataService(
    string filterGraphFolderPath,
    string mixerFolderPath,
    string preferencesFolderPath,
    string presetFolderPath) : IAppDataService
{
    private readonly AppDataServiceBase<FilterGraph>
        _filterGraphAppDataService =
            new(filterGraphFolderPath, "fc");

    private readonly AppDataServiceBase<Mixer>
        _mixerAppDataService = new(mixerFolderPath, "mix");

    private readonly AppDataServiceBase<FilterChainPreset>
        _presetAppDataService = new(presetFolderPath, "fcp");

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

    public Task CreateFilterChainPreset(FilterChainPreset preset) =>
        _presetAppDataService.Create(preset.Id, preset);

    public async Task<List<FilterChainPreset>> GetPresetsForFilterGraph(Guid filterGraphId)
    {
        var all = await _presetAppDataService.GetAll();
        return all.Where(p => p.FilterGraphId == filterGraphId).ToList();
    }

    public void DeleteFilterChainPreset(Guid id) =>
        _presetAppDataService.Delete(id);

    public async Task StorePreferences(
        Contracts.ApplicationPreferences.Preferences preferences)
    {
        var filePath = Path.Combine(preferencesFolderPath, "preferences.grpc");
        var file = File.Create(filePath);
        Serializer.Serialize(file, preferences);
        await file.FlushAsync();
        file.Close();
    }

    public async Task<Contracts.ApplicationPreferences.Preferences?>
        LoadPreferences()
    {
        var filePath = Path.Combine(preferencesFolderPath, "preferences.grpc");
        if (!File.Exists(filePath)) return null;

        var bytes = await File.ReadAllBytesAsync(filePath);
        using var memoryStream = new MemoryStream(bytes);
        return Serializer
            .Deserialize<Contracts.ApplicationPreferences.Preferences>(
                memoryStream);
    }

    public async Task StoreMidiSyncConfig(MidiSyncConfig config)
    {
        var filePath = Path.Combine(preferencesFolderPath, "midi-sync.grpc");
        await using var file = File.Create(filePath);
        Serializer.Serialize(file, config);
        await file.FlushAsync();
    }

    public async Task<MidiSyncConfig?> LoadMidiSyncConfig()
    {
        var filePath = Path.Combine(preferencesFolderPath, "midi-sync.grpc");
        if (!File.Exists(filePath)) return null;

        var bytes = await File.ReadAllBytesAsync(filePath);
        using var memoryStream = new MemoryStream(bytes);
        return Serializer.Deserialize<MidiSyncConfig>(memoryStream);
    }
}
