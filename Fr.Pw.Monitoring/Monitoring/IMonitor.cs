namespace Fr.Pw.Monitoring.Monitoring;

public interface IMonitor
{
    void StartMonitoring(ulong objectSerial);
    void StopMonitoring(ulong objectSerial);

    event Action<MonitoringUpdate> Updated;
}