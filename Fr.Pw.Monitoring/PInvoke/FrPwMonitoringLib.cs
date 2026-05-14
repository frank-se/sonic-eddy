using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Fr.Pw.Monitoring.Messages;

namespace Fr.Pw.Monitoring.PInvoke;

// ReSharper disable InconsistentNaming
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void PipewireMonitoringCallback(
    ulong object_serial,
    float left_peak, float right_peak,
    float left_average, float right_average);

internal static partial class FrPwMonitoringLib
{
    private const string LIBRARY_NAME = "libfrmonitoring.so.0.0.4";

    internal static BlockingCollection<ChannelMonitoringUpdateMessage>
        UpdatesFromPipewire { get; } =
        new();

    [LibraryImport(LIBRARY_NAME, EntryPoint = "init")]
    private static partial void InitC(
        PipewireMonitoringCallback callback,
        ulong update_interval_milliseconds);

    [LibraryImport(LIBRARY_NAME, EntryPoint = "start")]
    private static partial void StartC();

    [LibraryImport(LIBRARY_NAME, EntryPoint = "stop")]
    private static partial void StopC();

    [LibraryImport(LIBRARY_NAME, EntryPoint = "start_monitor_node")]
    private static partial void StartMonitorNodeC(ulong object_serial);

    [LibraryImport(LIBRARY_NAME, EntryPoint = "stop_monitor_node")]
    private static partial void StopMonitorNodeC(ulong object_serial);

    internal static void Init(TimeSpan updateInterval) =>
        InitC(MonitoringCallback, (ulong)updateInterval.Milliseconds);

    internal static void Start() => StartC();

    internal static void Stop() => StopC();

    internal static void StartMonitoringNode(ulong objectSerial) =>
        StartMonitorNodeC(objectSerial);

    internal static void StopMonitoringNode(ulong objectSerial) =>
        StopMonitorNodeC(objectSerial);

    private static void MonitoringCallback(ulong objectSerial, float leftPeak,
        float rightPeak, float leftAverage, float rightAverage)
    {
        UpdatesFromPipewire.Add(new(objectSerial, leftPeak, rightPeak,
            leftAverage, rightAverage));
    }
}