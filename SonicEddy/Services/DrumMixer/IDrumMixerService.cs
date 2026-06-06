using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fr.Sonic.Model.Objects;
using SonicEddy.Contracts.DrumMixer;

namespace SonicEddy.Services.DrumMixer;

public interface IDrumMixerService
{
    bool IsConfigured { get; }
    bool IsAvailable { get; }
    DrumMixerConfig State { get; }
    event Action? Changed;

    Task InitializeAsync();
    IReadOnlyList<Port> GetSourcePorts();
    Port? ResolveSource(int channelIndex);
    void SetSource(int channelIndex, Port? port);
    void SetChannelName(int channelIndex, string name);
    void SetChannelVolume(int channelIndex, float value);
    void SetSend(int channelIndex, bool room, float value);
    void SetReturnVolume(bool room, float value);
    void SetParameter(int channelIndex, string name, float value);
    void SetReturnParameter(bool room, string name, float value);
}
