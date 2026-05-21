using System.Runtime.InteropServices;

namespace Fr.Sonic.Sync;

public static partial class MonotonicClock
{
    private const int ClockMonotonic = 1;

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Timespec
    {
        public readonly long tv_sec;
        public readonly long tv_nsec;
    }

    [LibraryImport("libc", EntryPoint = "clock_gettime", SetLastError = true)]
    private static partial int ClockGetTime(int clockId, out Timespec tp);

    public static ulong NowNsec()
    {
        if (ClockGetTime(ClockMonotonic, out var ts) != 0)
            throw new InvalidOperationException(
                $"clock_gettime(CLOCK_MONOTONIC) failed: {Marshal.GetLastPInvokeError()}");

        return checked((ulong)ts.tv_sec * 1_000_000_000ul + (ulong)ts.tv_nsec);
    }
}
