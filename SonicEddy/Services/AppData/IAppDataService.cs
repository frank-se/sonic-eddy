using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Contracts.MidiRouter;
using SonicEddy.Contracts.MidiSync;
using SonicEddy.Contracts.Mixers;
using SonicEddy.Contracts.VirtualInputs;
using SonicEddy.Contracts.VirtualOutputs;
using SonicEddy.Contracts.ClickSync;
using SonicEddy.Contracts.DrumMixer;
using SonicEddy.Contracts.ExternalEffects;

namespace SonicEddy.Services.AppData;

public interface IAppDataService
{
    Task<FilterGraph> GetFilterGraph(Guid id);
    Task CreateFilterGraph(FilterGraph filterGraph);
    void DeleteFilterGraph(Guid id);
    Task<List<FilterGraph>> GetAllFilterGraphs();
    Task<List<Mixer>> GetAllMixers();
    Task<Mixer> GetMixer(Guid id);
    Task CreateMixer(Mixer mixer);
    void DeleteMixer(Guid id);

    Task CreateFilterChainPreset(FilterChainPreset preset);
    Task<List<FilterChainPreset>> GetPresetsForFilterGraph(Guid filterGraphId);
    void DeleteFilterChainPreset(Guid id);

    Task StorePreferences(
        Contracts.ApplicationPreferences.Preferences preferences);

    Task<Contracts.ApplicationPreferences.Preferences?> LoadPreferences();

    Task StoreMidiSyncConfig(MidiSyncConfig config);

    Task<MidiSyncConfig?> LoadMidiSyncConfig();

    Task StoreMidiRouterConfig(MidiRouterConfig config);

    Task<MidiRouterConfig?> LoadMidiRouterConfig();

    Task StoreVirtualInputsConfig(VirtualInputsConfig config);

    Task<VirtualInputsConfig?> LoadVirtualInputsConfig();

    Task StoreVirtualOutputsConfig(VirtualOutputsConfig config);

    Task<VirtualOutputsConfig?> LoadVirtualOutputsConfig();

    Task StoreClickSyncConfig(ClickSyncConfig config);

    Task<ClickSyncConfig?> LoadClickSyncConfig();

    Task StoreDrumMixerConfig(DrumMixerConfig config);

    Task<DrumMixerConfig?> LoadDrumMixerConfig();

    Task StoreExternalEffectsConfig(ExternalEffectsConfig config);

    Task<ExternalEffectsConfig?> LoadExternalEffectsConfig();
}
