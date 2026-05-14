using Fr.Pw.Monitoring.PInvoke;

namespace Fr.Pw.Monitoring.Monitoring;

public class Monitor : IMonitor
{
    private Thread? _updatesThread;

    public void StartMonitoring(ulong objectSerial) =>
        FrPwMonitoringLib.StartMonitoringNode(objectSerial);

    public void StopMonitoring(ulong objectSerial) =>
        FrPwMonitoringLib.StopMonitoringNode(objectSerial);

    public event Action<MonitoringUpdate>? Updated;

    internal void StartProcessing()
    {
        _updatesThread = new(() =>
        {
            while (true)
            {
                var item = FrPwMonitoringLib.UpdatesFromPipewire.Take();
                Updated?.Invoke(new(item.ObjectSerial,
                    [item.LeftPeak, item.RightPeak],
                    [item.LeftAverage, item.RightAverage]));
            }
        });
        _updatesThread.Start();
    }
}