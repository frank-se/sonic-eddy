using System;
using Fr.Pw.Monitoring.Monitoring;
using Fr.Wireplumber.Model.Objects;

namespace SonicEddy.Services.Monitoring;

public interface IMonitoringService
{
    void StartMonitoring(Node node);
    void StopMonitoring(Node node);

    event Action<MonitoringUpdate> Updated;
}