using System;

namespace SonicEddy.Services.Monitoring;

public interface IMonitoringLinkService
{
    void SetSource(MonitoringChannelKey key, MonitoringSource source);
    MonitoringSource GetSource(MonitoringChannelKey key);
    event Action? StateChanged;
}
