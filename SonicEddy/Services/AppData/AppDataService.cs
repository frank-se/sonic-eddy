using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ProtoBuf;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Contracts.MidiRouter;
using SonicEddy.Contracts.MidiSync;
using SonicEddy.Contracts.Mixers;
using SonicEddy.Contracts.VirtualInputs;
using SonicEddy.Contracts.VirtualOutputs;
using SonicEddy.Contracts.ClickSync;
using SonicEddy.Contracts.DrumMixer;
using SonicEddy.Contracts.ExternalEffects;
using SonicEddy.Contracts.RecordingPickUp;

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

    public async Task StoreMidiRouterConfig(MidiRouterConfig config)
    {
        var filePath = Path.Combine(preferencesFolderPath, "midi-router.grpc");
        await using var file = File.Create(filePath);
        Serializer.Serialize(file, config);
        await file.FlushAsync();
    }

    public async Task<MidiRouterConfig?> LoadMidiRouterConfig()
    {
        var filePath = Path.Combine(preferencesFolderPath, "midi-router.grpc");
        if (!File.Exists(filePath)) return null;

        var bytes = await File.ReadAllBytesAsync(filePath);
        using var memoryStream = new MemoryStream(bytes);
        return Serializer.Deserialize<MidiRouterConfig>(memoryStream);
    }

    public async Task StoreVirtualInputsConfig(VirtualInputsConfig config)
    {
        var filePath =
            Path.Combine(preferencesFolderPath, "virtual-inputs.grpc");
        await using var file = File.Create(filePath);
        Serializer.Serialize(file, config);
        await file.FlushAsync();
    }

    public async Task<VirtualInputsConfig?> LoadVirtualInputsConfig()
    {
        var filePath =
            Path.Combine(preferencesFolderPath, "virtual-inputs.grpc");
        if (!File.Exists(filePath)) return null;

        var bytes = await File.ReadAllBytesAsync(filePath);
        using var memoryStream = new MemoryStream(bytes);
        return Serializer.Deserialize<VirtualInputsConfig>(memoryStream);
    }

    public async Task StoreVirtualOutputsConfig(VirtualOutputsConfig config)
    {
        var filePath =
            Path.Combine(preferencesFolderPath, "virtual-outputs.grpc");
        await using var file = File.Create(filePath);
        Serializer.Serialize(file, config);
        await file.FlushAsync();
    }

    public async Task<VirtualOutputsConfig?> LoadVirtualOutputsConfig()
    {
        var filePath =
            Path.Combine(preferencesFolderPath, "virtual-outputs.grpc");
        if (!File.Exists(filePath)) return null;

        var bytes = await File.ReadAllBytesAsync(filePath);
        using var memoryStream = new MemoryStream(bytes);
        return Serializer.Deserialize<VirtualOutputsConfig>(memoryStream);
    }

    public async Task StoreClickSyncConfig(ClickSyncConfig config)
    {
        var filePath =
            Path.Combine(preferencesFolderPath, "click-sync.grpc");
        await using var file = File.Create(filePath);
        Serializer.Serialize(file, config);
        await file.FlushAsync();
    }

    public async Task<ClickSyncConfig?> LoadClickSyncConfig()
    {
        var filePath =
            Path.Combine(preferencesFolderPath, "click-sync.grpc");
        if (!File.Exists(filePath)) return null;

        var bytes = await File.ReadAllBytesAsync(filePath);
        using var memoryStream = new MemoryStream(bytes);
        return Serializer.Deserialize<ClickSyncConfig>(memoryStream);
    }

    public async Task StoreDrumMixerConfig(DrumMixerConfig config)
    {
        var filePath =
            Path.Combine(preferencesFolderPath, "drum-mixer.grpc");
        await using var file = File.Create(filePath);
        Serializer.Serialize(file, config);
        await file.FlushAsync();
    }

    public async Task<DrumMixerConfig?> LoadDrumMixerConfig()
    {
        var filePath =
            Path.Combine(preferencesFolderPath, "drum-mixer.grpc");
        if (!File.Exists(filePath)) return null;

        var bytes = await File.ReadAllBytesAsync(filePath);
        using var memoryStream = new MemoryStream(bytes);
        return Serializer.Deserialize<DrumMixerConfig>(memoryStream);
    }

    public async Task StoreExternalEffectsConfig(ExternalEffectsConfig config)
    {
        var filePath =
            Path.Combine(preferencesFolderPath, "external-effects.grpc");
        await using var file = File.Create(filePath);
        Serializer.Serialize(file, config);
        await file.FlushAsync();
    }

    public async Task<ExternalEffectsConfig?> LoadExternalEffectsConfig()
    {
        var filePath =
            Path.Combine(preferencesFolderPath, "external-effects.grpc");
        if (!File.Exists(filePath)) return null;

        var bytes = await File.ReadAllBytesAsync(filePath);
        using var memoryStream = new MemoryStream(bytes);
        return Serializer.Deserialize<ExternalEffectsConfig>(memoryStream);
    }

    public async Task StoreRecordingPickUpConfig(RecordingPickUpConfig config)
    {
        var filePath =
            Path.Combine(preferencesFolderPath, "recording-pickups.grpc");
        await using var file = File.Create(filePath);
        Serializer.Serialize(file, config);
        await file.FlushAsync();
    }

    public async Task<RecordingPickUpConfig?> LoadRecordingPickUpConfig()
    {
        var filePath =
            Path.Combine(preferencesFolderPath, "recording-pickups.grpc");
        if (!File.Exists(filePath)) return null;

        var bytes = await File.ReadAllBytesAsync(filePath);
        using var memoryStream = new MemoryStream(bytes);
        return Serializer.Deserialize<RecordingPickUpConfig>(memoryStream);
    }
}
