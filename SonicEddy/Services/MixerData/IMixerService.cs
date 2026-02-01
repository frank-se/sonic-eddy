using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fr.Wireplumber.Model.Objects;
using SonicEddy.Contracts.FilterGraph;

namespace SonicEddy.Services.MixerData;

public interface IMixerService
{
    public Mixer CurrentMixer { get; }

    public Mixer NewCurrentMixer(string name);

    public Task<Mixer> AddChannelStripToCurrentMixer(string name,
        Node inputNode);

    public Task<ChannelStrip> AddFilterToChannelStrip(ulong channelId,
        FilterGraph filterGraph);

    public Task<Guid> PersistCurrentMixer();

    public Task<List<Contracts.Mixers.Mixer>> GetAllMixers();

    public Task<Mixer> RestoreMixer(Guid id);

    public void DeleteMixer(Guid id);
}