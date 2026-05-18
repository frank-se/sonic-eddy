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

    internal static void OnPeak(ulong objectSerial, float leftPeak, float rightPeak,
        float leftAverage, float rightAverage)
    {
        var update = new MonitoringUpdate(objectSerial, leftPeak, rightPeak,
            leftAverage, rightAverage);
        FrSonic.MonitoringEvents.Add(update);
        PeakChanged?.Invoke(null, update);
    }

    public static void StartMonitorNode(ulong objectSerial) =>
        FrSonicLib.StartMonitorNodeC(objectSerial);

    public static void StopMonitorNode(ulong objectSerial) =>
        FrSonicLib.StopMonitorNodeC(objectSerial);
}
