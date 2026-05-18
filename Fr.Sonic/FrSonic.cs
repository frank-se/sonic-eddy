using System.Collections.Concurrent;
using Fr.Sonic.PInvoke;

namespace Fr.Sonic;

/// <summary>
/// Unified static facade for the fr-sonic native backend.
/// Call <see cref="Init"/> then <see cref="Start"/> before using any subsystem.
/// </summary>
public static class FrSonic
{
    /* ── queues ─────────────────────────────────────────────────────────── */

    internal static readonly BlockingCollection<object> WireplumberEvents = new();
    internal static readonly BlockingCollection<object> MonitoringEvents  = new();

    /* ── lifecycle ───────────────────────────────────────────────────────── */

    public static void Init(TimeSpan peakUpdateInterval)
    {
        FrSonicLib.InitC(
            FrSonicWireplumber.OnNodeAdded,
            FrSonicWireplumber.OnPropsChanged,
            FrSonicWireplumber.OnPropsEnumFailed,
            FrSonicWireplumber.OnPropInfoAdded,
            FrSonicWireplumber.OnObjectDeleted,
            FrSonicWireplumber.OnMetadataAdded,
            FrSonicWireplumber.OnMetadataEntryUpdated,
            FrSonicWireplumber.OnMetadataEntryDeleted,
            FrSonicMonitoring.OnPeak,
            (ulong)peakUpdateInterval.TotalMilliseconds,
            FrSonicMidi.OnMidiCcUpdate);
    }

    public static void Start() => FrSonicLib.StartC();
    public static void Stop()  => FrSonicLib.StopC();
}
