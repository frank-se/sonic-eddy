using System;
using Fr.Sonic.Monitoring;
using Fr.Sonic.Model.Objects;
using Microsoft.Extensions.Logging;

namespace SonicEddy.Services.Monitoring;

public class MonitoringService : IMonitoringService, IDisposable
{
    private readonly IMonitor _monitor;
    private readonly ILogger<MonitoringService> _logger;

    public MonitoringService(IMonitor monitor, ILogger<MonitoringService> logger)
    {
        _monitor = monitor;
        _logger = logger;
        monitor.Updated += Forward;
    }

    public void StartMonitoring(Node node)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("StartMonitoring serial={Serial}", node.ObjectSerial);
        _monitor.StartMonitoring(node.ObjectSerial);
    }

    public void StopMonitoring(Node node)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("StopMonitoring serial={Serial}", node.ObjectSerial);
        _monitor.StopMonitoring(node.ObjectSerial);
    }

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