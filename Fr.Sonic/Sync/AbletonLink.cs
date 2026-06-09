using Fr.Sonic.PInvoke;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Fr.Sonic.Sync;

public static class AbletonLink
{
    private static volatile int _numPeers;

    public static event EventHandler<int>? NumPeersChanged;

    public static bool IsEnabled
    {
        get => FrSonicLib.LinkIsEnabled();
        set => FrSonicLib.LinkEnable(value);
    }

    public static int NumPeers => _numPeers;

    public static double Quantum
    {
        set => FrSonicLib.LinkSetQuantum(value);
    }

    public static void Initialize()
    {
        FrSonicLib.LinkSetPeersCallback(
            Marshal.GetFunctionPointerForDelegate<PeersChangedDelegate>(OnPeersChanged));
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void PeersChangedDelegate(nuint peers);

    // Keep delegate alive — GC must not collect it.
    private static readonly PeersChangedDelegate OnPeersChanged = peers =>
    {
        _numPeers = (int)peers;
        NumPeersChanged?.Invoke(null, _numPeers);
    };
}
