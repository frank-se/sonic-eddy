using System;
using Fr.Sonic.Model.Objects;

namespace SonicEddy.Services.Monitoring;

public interface IMonitoringLinkService
{
    void SetSource(MonitoringChannelKey key, MonitoringSource source);
    MonitoringSource GetSource(MonitoringChannelKey key);
    Node? ResolvePickUpNode(MonitoringChannelKey key, MonitoringSource source);
    event Action? StateChanged;
}
