using Fr.Sonic.PInvoke;

namespace Fr.Sonic.Monitoring;

public class Monitor : IMonitor
{
    private Thread? _updatesThread;

    public void StartMonitoring(ulong objectSerial) =>
        FrSonicLib.StartMonitorNodeC(objectSerial);

    public void StopMonitoring(ulong objectSerial) =>
        FrSonicLib.StopMonitorNodeC(objectSerial);

    public event Action<MonitoringUpdate>? Updated;

    internal void StartProcessing()
    {
        _updatesThread = new(() =>
        {
            try
            {
                foreach (var item in FrSonicMonitoring.MonitoringQueue.GetConsumingEnumerable())
                    Updated?.Invoke(new(item.ObjectSerial,
                        [item.LeftPeak, item.RightPeak],
                        [item.LeftAverage, item.RightAverage]));
            }
            catch (OperationCanceledException) { }
        })
        { IsBackground = true };
        _updatesThread.Start();
    }
}
