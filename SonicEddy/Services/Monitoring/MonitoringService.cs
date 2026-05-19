using System;
using Fr.Sonic.Monitoring;
using Fr.Sonic.Model.Objects;

namespace SonicEddy.Services.Monitoring;

public class MonitoringService : IMonitoringService, IDisposable
{
    private readonly IMonitor _monitor;

    public MonitoringService(IMonitor monitor)
    {
        _monitor = monitor;
        monitor.Updated += Forward;
    }

    public void StartMonitoring(Node node) =>
        _monitor.StartMonitoring(node.ObjectSerial);

    public void StopMonitoring(Node node) =>
        _monitor.StopMonitoring(node.ObjectSerial);
    
    public event Action<MonitoringUpdate>? Updated;

    private void Forward(MonitoringUpdate update)
    {
        Updated?.Invoke(update);
    }

    public void Dispose()
    {
        _monitor.Updated -= Forward;
        GC.SuppressFinalize(this);
    }
}