using Fr.Pw.Monitoring.Monitoring;
using Fr.Pw.Monitoring.PInvoke;
using Monitor = Fr.Pw.Monitoring.Monitoring.Monitor;

namespace Fr.Pw.Monitoring;

public static class FrPwMonitoring
{
    // ReSharper disable once InconsistentNaming
    private static readonly Monitor _monitor = new Monitor();

    public static IMonitor Monitor => _monitor;

    public static void Start(TimeSpan interval)
    {
        FrPwMonitoringLib.Init(interval);
        FrPwMonitoringLib.Start();
        _monitor.StartProcessing();
    }

    public static void Stop()
    {
        FrPwMonitoringLib.Stop();
    }
}