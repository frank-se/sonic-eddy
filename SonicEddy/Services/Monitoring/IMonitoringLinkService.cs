using System;
using Fr.Sonic.Modules.Models;
using SonicEddy.Services.MixerServiceV2;

namespace SonicEddy.Services.Monitoring;

public interface IMonitoringLinkService
{
    void SetMixer(Mixer? mixer);
    void SetMonitoringLoopback(LoopbackModule? loopback);
    void SetSource(MonitoringChannelKey key, MonitoringSource source);
    MonitoringSource GetSource(MonitoringChannelKey key);
    event Action? StateChanged;
}
