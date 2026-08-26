using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace SonicEddy.Services.SoomfonDeck;

/// <summary>
/// Protocol ported from the proven SoomfonDeckTest/Program.cs (see the plan
/// this was built from for the full writeup, reimplemented clean-room in C#
/// from the documented behavior of the open-source mirajazz/opendeck-akp153
/// Rust project - VID 0x1500/PID 0x3003 matches Kind::SFSTC "Soomfon Stream
/// Controller" in that project's device table, protocol version 3).
/// Structurally mirrors TraktorZ1Service (plain FileStream over hidraw, one
/// background read thread), except this device exposes two matching hidraw
/// interfaces at once - both accept writes, only one produces real "ACK"-
/// prefixed input reports (confirmed empirically), so this keeps both open
/// and listens on both rather than guessing which is which.
/// </summary>
public sealed class SoomfonDeckService : ISoomfonDeckService
{
    private const int PacketSize = 1024; // protocol version 3 (this device)
    private const int InputReportSize = 512;

    // AKP153-family PCB's fixed physical-wiring-to-logical-grid mapping
    // (3x6=18 keys), confirmed empirically against this specific unit.
    private static readonly int[] DeviceToLogical =
        [4, 10, 16, 3, 9, 15, 2, 8, 14, 1, 7, 13, 0, 6, 12, 5, 11, 17];

    // Logical grid index -> device's own physical key index for addressing
    // images/clears - not simply the inverse of DeviceToLogical, a distinct
    // mapping per the akp153-family plugin's own functions, ported verbatim.
    private static readonly int[] LogicalToDevice =
        [12, 9, 6, 3, 0, 15, 13, 10, 7, 4, 1, 16, 14, 11, 8, 5, 2, 17];

    private readonly ILogger<SoomfonDeckService> _logger;
    private readonly object _gate = new();
    private readonly List<(string Path, FileStream Stream, Thread Thread)> _connections = [];
    private CancellationTokenSource? _cts;

    public SoomfonDeckService(ILogger<SoomfonDeckService> logger) => _logger = logger;

    public event Action<int, bool>? KeyStateChanged;

    public void Start(IReadOnlyList<string> hidrawPaths)
    {
        Stop();
        if (hidrawPaths.Count == 0) return;

        var cts = new CancellationTokenSource();
        _cts = cts;

        lock (_gate)
        {
            foreach (var path in hidrawPaths)
            {
                FileStream stream;
                try
                {
                    stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Could not open Soomfon deck at {Path}: {Message}", path, ex.Message);
                    continue;
                }

                try
                {
                    SendInitSequence(stream);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Soomfon deck init failed on {Path}: {Message}", path, ex.Message);
                }

                var thread = new Thread(() => ReadLoop(path, stream, cts.Token))
                {
                    IsBackground = true,
                    Name = $"SoomfonRead-{Path.GetFileName(path)}",
                };
                _connections.Add((path, stream, thread));
                thread.Start();
            }
        }

        if (_connections.Count > 0)
            _logger.LogInformation("Soomfon deck started on {Paths}", string.Join(", ", hidrawPaths));
    }

    public void Stop()
    {
        _cts?.Cancel();

        List<(string Path, FileStream Stream, Thread Thread)> connections;
        lock (_gate)
        {
            connections = [.. _connections];
            _connections.Clear();
        }

        foreach (var (_, stream, thread) in connections)
        {
            stream.Dispose();
            thread.Join(TimeSpan.FromSeconds(1));
        }

        _cts?.Dispose();
        _cts = null;
    }

    // Single atomic operation covering a whole 15-key repaint (push/clear
    // every key, then commit) rather than separate PushImage/ClearKey/
    // Commit calls - keeps _writeGate held for the entire frame, so two
    // overlapping repaints (e.g. the caller not waiting for one Task.Run'd
    // paint to finish before starting the next) can't interleave and leave
    // some keys from one frame mixed with some from another.
    public void PaintFrame(IReadOnlyList<(int Key, byte[]? Image)> keys) =>
        ForEachStream((path, stream) =>
        {
            foreach (var (key, image) in keys)
            {
                var deviceKey = (byte)LogicalToDevice[key];
                if (image is not null)
                {
                    SendImageHeader(stream, deviceKey, image.Length);
                    WriteImageDataReports(stream, image);
                }
                else
                {
                    ClearButtonImage(stream, (byte)(deviceKey + 1));
                }
            }

            SendCommand(stream, Encoding.ASCII.GetBytes("STP"));
        }, "paint frame");

    // Guards actual writes (separate from _gate, which only guards the
    // _connections list) so two overlapping frames can't interleave chunks
    // onto the same stream and garble a half-written image.
    private readonly object _writeGate = new();

    private void ForEachStream(Action<string, FileStream> action, string what)
    {
        List<(string Path, FileStream Stream)> streams;
        lock (_gate)
            streams = _connections.Select(c => (c.Path, c.Stream)).ToList();

        lock (_writeGate)
        {
            foreach (var (path, stream) in streams)
            {
                try
                {
                    action(path, stream);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Soomfon deck {What} failed on {Path}: {Message}", what, path, ex.Message);
                }
            }
        }
    }

    // Every command: [0x00, 'C','R','T', 0x00, 0x00, <3-letter tag>, ...params],
    // zero-padded to 1 + PacketSize bytes and written as one output report.
    private static void SendCommand(FileStream stream, byte[] tagAndParams)
    {
        var buf = new byte[1 + PacketSize];
        buf[0] = 0x00;
        Encoding.ASCII.GetBytes("CRT").CopyTo(buf, 1);
        buf[4] = 0x00;
        buf[5] = 0x00;
        tagAndParams.CopyTo(buf, 6);
        stream.Write(buf, 0, buf.Length);
        stream.Flush();
    }

    private static void SendInitSequence(FileStream stream)
    {
        SendCommand(stream, Encoding.ASCII.GetBytes("DIS"));
        SendCommand(stream, [.. Encoding.ASCII.GetBytes("LIG"), 0x00, 0x00, 0x00, 0x00]);
        SetBrightness(stream, 50);
        ClearAllButtonImages(stream);
    }

    private static void SetBrightness(FileStream stream, byte percent) =>
        SendCommand(stream, [.. Encoding.ASCII.GetBytes("LIG"), 0x00, 0x00, percent]);

    private static void ClearButtonImage(FileStream stream, byte keyParam) =>
        SendCommand(stream, [.. Encoding.ASCII.GetBytes("CLE"), 0x00, 0x00, 0x00, keyParam]);

    private static void ClearAllButtonImages(FileStream stream)
    {
        ClearButtonImage(stream, 0xFF);
        SendCommand(stream, Encoding.ASCII.GetBytes("STP")); // commit, protocol >= 2
    }

    private static void SendImageHeader(FileStream stream, byte deviceKey, int length) =>
        SendCommand(stream, [
            .. Encoding.ASCII.GetBytes("BAT"), 0x00, 0x00,
            (byte)(length >> 8), (byte)length, (byte)(deviceKey + 1),
        ]);

    // Image data has no "CRT" command wrapper - just raw chunks, each
    // prefixed with a single 0x00 report-id byte, padded to the full
    // report length.
    private static void WriteImageDataReports(FileStream stream, byte[] imageData)
    {
        const int reportLength = PacketSize + 1;
        const int payloadLength = PacketSize;

        var pageNumber = 0;
        var bytesRemaining = imageData.Length;
        while (bytesRemaining > 0)
        {
            var thisLength = Math.Min(bytesRemaining, payloadLength);
            var bytesSent = pageNumber * payloadLength;

            var buf = new byte[reportLength];
            Array.Copy(imageData, bytesSent, buf, 1, thisLength);

            stream.Write(buf, 0, buf.Length);
            stream.Flush();

            bytesRemaining -= thisLength;
            pageNumber++;
        }
    }

    private void ReadLoop(string path, FileStream stream, CancellationToken token)
    {
        var buf = new byte[InputReportSize];
        try
        {
            while (!token.IsCancellationRequested)
            {
                var n = stream.Read(buf, 0, buf.Length);
                if (n != buf.Length) continue;

                // "ACK" prefix (protocol version > 0, which this device is)
                if (buf[0] != 0x41 || buf[1] != 0x43 || buf[2] != 0x4B) continue;

                var rawKey = buf[9];
                var state = buf[10];
                if (rawKey == 0) continue; // heartbeat/no-change

                if (rawKey < 1 || rawKey > DeviceToLogical.Length) continue;

                var logicalIndex = DeviceToLogical[rawKey - 1];
                KeyStateChanged?.Invoke(logicalIndex, state != 0);
            }
        }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
    }

    public void Dispose() => Stop();
}
