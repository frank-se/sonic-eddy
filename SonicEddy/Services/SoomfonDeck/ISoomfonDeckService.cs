using System;
using System.Collections.Generic;

namespace SonicEddy.Services.SoomfonDeck;

/// <summary>
/// Talks to a Soomfon XF-CN001 stream deck over hidraw - see
/// SoomfonDeckTest/Program.cs (and the plan this was built from) for the
/// full protocol writeup. Key indices throughout are the logical 0-17 grid
/// index (3 rows of 6), not the device's raw wire index - callers never
/// need to know about that permutation.
/// </summary>
public interface ISoomfonDeckService : IDisposable
{
    void Start(IReadOnlyList<string> hidrawPaths);
    void Stop();

    /// Fires from a background read thread - subscribers must marshal to
    /// the UI thread themselves before touching bound properties.
    event Action<int, bool>? KeyStateChanged; // (logicalKey, isDown)

    /// Paints (or clears, for a null image) every listed key and commits in
    /// one atomic operation - callers should build a full frame and call
    /// this once, not push/clear keys individually, so two overlapping
    /// repaints from different threads can't interleave and garble the
    /// device's display. This does blocking hidraw I/O - call it from a
    /// background thread, never the UI thread.
    void PaintFrame(IReadOnlyList<(int Key, byte[]? Image)> keys);
}
