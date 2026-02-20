using System;
using System.Threading.Tasks;
using Fr.Wireplumber.Model.Objects;
using SonicEddy.Contracts.FilterGraph;

namespace SonicEddy.Services.MixerServiceV2;

public interface IMixerService
{
    public Mixer? CurrentMixer { get; }

    public Task<Mixer> NewCurrentMixer(string name);

    public Task<ChannelStrip> AddFilterToChannelStrip(ulong channelId,
        FilterGraph filterGraph);

    public Task<Guid> PersistCurrentMixer();

    public Task<Mixer> RestoreMixer(Guid id);

    public void DeleteMixer(Guid id);
}