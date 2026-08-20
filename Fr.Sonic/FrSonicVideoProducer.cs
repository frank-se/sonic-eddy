using System.Runtime.InteropServices;
using Fr.Sonic.PInvoke;

namespace Fr.Sonic;

/// <summary>
/// A single fixed-size RGBA PipeWire video source node fed by UpdateFrame().
/// UpdateFrame is real-time-safe to call from any thread - it never blocks
/// on PipeWire, it only copies into a scratch buffer the native process()
/// callback reads from.
/// </summary>
public sealed class FrSonicVideoProducer : IDisposable
{
    private readonly UIntPtr _handle;
    private readonly uint _width;
    private readonly uint _height;
    private bool _disposed;

    private FrSonicVideoProducer(UIntPtr handle, uint width, uint height)
    {
        _handle = handle;
        _width = width;
        _height = height;
    }

    public static FrSonicVideoProducer? Create(
        string name, string description, uint width, uint height)
    {
        using var nameStr = NativeUtf8String.From(name);
        using var descriptionStr = NativeUtf8String.From(description);

        var config = new FrSonicVideoProducerConfig
        {
            Name = nameStr.Pointer,
            Description = descriptionStr.Pointer,
            Width = width,
            Height = height,
        };

        return FrSonicLib.CreateVideoProducerC(config, out var handle)
            ? new FrSonicVideoProducer(handle, width, height)
            : null;
    }

    public bool UpdateFrame(ReadOnlySpan<byte> rgba)
    {
        if (_disposed) return false;

        var expected = (long)_width * _height * 4;
        if (rgba.Length != expected) return false;

        return FrSonicLib.UpdateVideoProducerFrameC(
            _handle, rgba, (nuint)rgba.Length);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        FrSonicLib.DestroyVideoProducerC(_handle);
    }

    private sealed class NativeUtf8String : IDisposable
    {
        private NativeUtf8String(IntPtr pointer) => Pointer = pointer;

        internal IntPtr Pointer { get; private set; }

        internal static NativeUtf8String From(string value) =>
            new(Marshal.StringToCoTaskMemUTF8(value));

        public void Dispose()
        {
            if (Pointer == IntPtr.Zero) return;
            Marshal.FreeCoTaskMem(Pointer);
            Pointer = IntPtr.Zero;
        }
    }
}
