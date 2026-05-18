using System.Collections.Concurrent;
using Fr.Sonic.Monitoring.Messages;
using Fr.Sonic.PInvoke;

namespace Fr.Sonic;

public record MonitoringUpdate(
    ulong ObjectSerial,
    float LeftPeak, float RightPeak,
    float LeftAverage, float RightAverage);

/// <summary>
/// Peak-level monitoring facade.
/// </summary>
public static class FrSonicMonitoring
{
    public static event EventHandler<MonitoringUpdate>? PeakChanged;

    internal static readonly BlockingCollection<ChannelMonitoringUpdateMessage>
        MonitoringQueue = new();

    internal static void OnPeak(ulong objectSerial, float leftPeak, float rightPeak,
        float leftAverage, float rightAverage)
    {
        var update = new MonitoringUpdate(objectSerial, leftPeak, rightPeak,
            leftAverage, rightAverage);
        MonitoringQueue.Add(new(objectSerial, leftPeak, rightPeak, leftAverage, rightAverage));
        PeakChanged?.Invoke(null, update);
    }

    public static void StartMonitorNode(ulong objectSerial) =>
        FrSonicLib.StartMonitorNodeC(objectSerial);

    public static void StopMonitorNode(ulong objectSerial) =>
        FrSonicLib.StopMonitorNodeC(objectSerial);
}
