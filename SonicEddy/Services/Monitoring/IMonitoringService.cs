using System;
using Fr.Sonic.Monitoring;
using Fr.Sonic.Model.Objects;

namespace SonicEddy.Services.Monitoring;

public interface IMonitoringService
{
    void StartMonitoring(Node node);
    void StopMonitoring(Node node);

    event Action<MonitoringUpdate> Updated;
}