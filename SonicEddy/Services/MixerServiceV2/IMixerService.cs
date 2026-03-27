using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    event Action<List<InputChannel>>? InputsChanged;
    event Action<List<OutputChannel>>? OutputsChanged;
}