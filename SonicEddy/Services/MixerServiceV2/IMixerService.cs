using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fr.Sonic.Modules.Models;
using SonicEddy.Contracts.FilterGraph;

namespace SonicEddy.Services.MixerServiceV2;

public interface IMixerService
{
    public Mixer? CurrentMixer { get; }

    public Task<Mixer> NewCurrentMixer(string name);

    public Task<Mixer?> GetAndLock();
    public Task Unlock();

    public Task<ChannelStrip> AddFilterToChannelStrip(
        int layerId,
        ulong channelId,
        FilterGraph filterGraph);

    public Task<FilterChain?> AddFilterToGroupChannel(
        int layerId,
        ulong channelId,
        FilterGraph filterGraph);

    public Task<FilterChain?> AddFilterToMasterChannel(
        int layerId,
        FilterGraph filterGraph);

    Task RemoveFilterFromChannelStrip(int layerId, ulong channelId);
    Task RemoveFilterFromGroupChannel(int layerId, ulong channelId);
    Task RemoveFilterFromMasterChannel(int layerId);
    Task<FilterChain?> AddFilterToReturnChannel(int layerId, int index,
        FilterGraph filterGraph);
    Task RemoveFilterFromReturnChannel(int layerId, int index);
    Task<InsertProcessor?> AddExternalEffectToChannelStrip(int layerId,
        ulong channelId, Guid effectId);
    Task<InsertProcessor?> AddExternalEffectToGroupChannel(int layerId,
        ulong channelId, Guid effectId);
    Task<InsertProcessor?> AddExternalEffectToMasterChannel(int layerId,
        Guid effectId);
    Task<InsertProcessor?> AddExternalEffectToReturnChannel(int layerId,
        int index, Guid effectId);
    Task<InsertProcessor?> SetGlobalMasterInsertProcessor(
        InsertProcessorRequest? request);
    void SetGlobalMasterOutputTarget(ulong objectSerial);

    Task<FilterChain?> AddFilterToMicChannel(int micIndex,
        FilterGraph filterGraph);
    Task RemoveFilterFromMicChannel(int micIndex);
    Task<InsertProcessor?> AddExternalEffectToMicChannel(int micIndex,
        Guid effectId);

    event Action<List<InputChannel>>? InputsChanged;
    event Action<List<OutputChannel>>? OutputsChanged;

    int NumberOfChannelsPerLayer { get; }
    int NumberOfChannels { get; }
    int NumberOfGroupChannelsPerLayer { get; }
    int NumberOfGroupChannels { get; }
}
