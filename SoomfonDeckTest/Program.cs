// Standalone diagnostic tool: proves basic HID communication with a Soomfon
// XF-CN001 stream-deck-style macro pad before any real SonicEddy feature is
// built on top of it. See /home/frank/.claude/plans/i-want-to-implement-zesty-scott.md
// for the full protocol writeup this was reimplemented from (clean-room, in
// C#, from the documented behavior of the open-source mirajazz/opendeck-akp153
// Rust project - no Rust code copied). Deliberately standalone and disposable,
// matching pw-video-compositor/test/*.cpp's role in this repo: prove a new
// protocol works before committing to a real integration.
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

const ushort VendorId = 0x1500;
const ushort ProductId = 0x3003;

var candidatePaths = FindHidrawPaths(VendorId, ProductId);
if (candidatePaths.Count == 0)
{
    Console.WriteLine($"No hidraw device found for VID {VendorId:X4}:PID {ProductId:X4}. Is it plugged in?");
    return 1;
}

Console.WriteLine($"Found {candidatePaths.Count} candidate hidraw path(s): {string.Join(", ", candidatePaths)}");

var openStreams = new List<(string Path, FileStream Stream)>();
foreach (var path in candidatePaths)
{
    try
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        openStreams.Add((path, stream));
        Console.WriteLine($"Opened {path}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Could not open {path}: {ex.Message}");
    }
}

if (openStreams.Count == 0)
{
    Console.WriteLine("Failed to open any candidate hidraw path.");
    return 1;
}

// We don't yet know from .NET which of the (possibly multiple) HID
// interfaces is the vendor command/report one, so send the init sequence to
// all of them - unsupported interfaces are expected to either ignore it or
// throw, which we log and move past rather than treat as fatal.
foreach (var (path, stream) in openStreams)
{
    try
    {
        SendInitSequence(stream);
        Console.WriteLine($"{path}: init sequence + brightness + clear sent");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{path}: init sequence failed: {ex.Message}");
    }
}

// Prove image push works too: send a distinct solid-color test JPEG to a
// normal key (logical index 0, 95x95) and to one segment of the display-only
// strip (logical index 5, 82x82 - see get_image_format_for_key in the
// akp153-family plugin this was ported from). No physical button exists
// behind 5/11/17 (confirmed empirically - they never appear in ReadLoop
// output), but they're still addressable for images.
foreach (var (path, stream) in openStreams)
{
    try
    {
        SendImage(stream, logicalKey: 0, BuildTestImageJpeg(95, 95, Color.Red));
        SendImage(stream, logicalKey: 5, BuildTestImageJpeg(82, 82, Color.Blue));
        SendCommand(stream, Encoding.ASCII.GetBytes("STP")); // commit, protocol >= 2
        Console.WriteLine($"{path}: test images sent (key 0 red, strip segment 5 blue)");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{path}: image push failed: {ex.Message}");
    }
}

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var readThreads = openStreams.Select(entry => new Thread(() => ReadLoop(entry.Path, entry.Stream, cts.Token))
{
    IsBackground = true,
    Name = $"SoomfonRead-{Path.GetFileName(entry.Path)}",
}).ToList();

foreach (var thread in readThreads)
    thread.Start();

Console.WriteLine("Reading input reports - press keys on the device (Ctrl+C to quit)...");
cts.Token.WaitHandle.WaitOne();

foreach (var thread in readThreads)
    thread.Join(TimeSpan.FromSeconds(1));

foreach (var (path, stream) in openStreams)
{
    try
    {
        SendShutdown(stream);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{path}: shutdown command failed: {ex.Message}");
    }

    stream.Dispose();
}

return 0;

static List<string> FindHidrawPaths(ushort vendorId, ushort productId)
{
    const string hidrawRoot = "/sys/class/hidraw";
    var results = new List<string>();
    if (!Directory.Exists(hidrawRoot)) return results;

    foreach (var dir in Directory.EnumerateDirectories(hidrawRoot))
    {
        if (!TryReadHidId(Path.Combine(dir, "device", "uevent"), out var vendor, out var product))
            continue;

        if (vendor == vendorId && product == productId)
            results.Add($"/dev/{Path.GetFileName(dir)}");
    }

    return results;
}

// Format: HID_ID=<bus>:<vendor>:<product>, each hex, vendor/product
// zero-padded to 8 digits, e.g. HID_ID=0003:00001500:00003003
static bool TryReadHidId(string uEventPath, out ushort vendor, out ushort product)
{
    vendor = product = 0;
    if (!File.Exists(uEventPath)) return false;

    string[] lines;
    try
    {
        lines = File.ReadAllLines(uEventPath);
    }
    catch (IOException) { return false; }
    catch (UnauthorizedAccessException) { return false; }

    var line = lines.FirstOrDefault(l => l.StartsWith("HID_ID=", StringComparison.Ordinal));
    if (line is null) return false;

    var parts = line["HID_ID=".Length..].Split(':');
    if (parts.Length != 3) return false;

    return ushort.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out vendor) &&
           ushort.TryParse(parts[2], System.Globalization.NumberStyles.HexNumber, null, out product);
}

// Every command: [0x00, 'C','R','T', 0x00, 0x00, <3-letter tag>, ...params],
// zero-padded to 1 + PacketSize bytes and written as one output report.
static void SendCommand(FileStream stream, byte[] tagAndParams)
{
    const int packetSize = 1024; // protocol version 3 (this device)
    var buf = new byte[1 + packetSize];
    buf[0] = 0x00;
    Encoding.ASCII.GetBytes("CRT").CopyTo(buf, 1);
    buf[4] = 0x00;
    buf[5] = 0x00;
    tagAndParams.CopyTo(buf, 6);
    stream.Write(buf, 0, buf.Length);
    stream.Flush();
}

static void SendInitSequence(FileStream stream)
{
    SendCommand(stream, Encoding.ASCII.GetBytes("DIS"));
    SendCommand(stream, [.. Encoding.ASCII.GetBytes("LIG"), 0x00, 0x00, 0x00, 0x00]);
    SetBrightness(stream, 50);
    ClearAllButtonImages(stream);
}

static void SetBrightness(FileStream stream, byte percent) =>
    SendCommand(stream, [.. Encoding.ASCII.GetBytes("LIG"), 0x00, 0x00, percent]);

static void ClearButtonImage(FileStream stream, byte key) =>
    SendCommand(stream, [.. Encoding.ASCII.GetBytes("CLE"), 0x00, 0x00, 0x00, key]);

static void ClearAllButtonImages(FileStream stream)
{
    ClearButtonImage(stream, 0xFF);
    SendCommand(stream, Encoding.ASCII.GetBytes("STP")); // commit, protocol >= 2
}

static void SendImage(FileStream stream, int logicalKey, byte[] jpegBytes)
{
    // Logical grid index (0-17, same indexing ReadLoop reports) -> device's
    // own physical key index. Not simply the inverse of ReadLoop's
    // permutation table - a distinct mapping per the akp153-family plugin's
    // own opendeck_to_device/device_to_opendeck functions, ported verbatim.
    int[] logicalToDevice = [12, 9, 6, 3, 0, 15, 13, 10, 7, 4, 1, 16, 14, 11, 8, 5, 2, 17];

    var deviceKey = (byte)logicalToDevice[logicalKey];
    var len = jpegBytes.Length;

    SendCommand(stream, [
        .. Encoding.ASCII.GetBytes("BAT"), 0x00, 0x00,
        (byte)(len >> 8), (byte)len, (byte)(deviceKey + 1),
    ]);

    WriteImageDataReports(stream, jpegBytes);
}

// Image data has no "CRT" command wrapper - just raw chunks, each prefixed
// with a single 0x00 report-id byte, padded to the full report length.
static void WriteImageDataReports(FileStream stream, byte[] imageData)
{
    const int packetSize = 1024; // protocol version 3 (this device)
    const int reportLength = packetSize + 1;
    const int payloadLength = packetSize;

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

// Matches the akp153-family plugin's own image pipeline for this protocol
// version: resize to the exact per-key size, rotate 90 (clockwise), then
// mirror both axes, then JPEG-encode at quality 90. Rotation/mirroring is
// invisible on a flat solid color, but implemented correctly anyway so this
// is directly reusable once real per-key labels/icons are needed.
static byte[] BuildTestImageJpeg(int width, int height, Color color)
{
    using var image = new Image<Rgb24>(width, height, color.ToPixel<Rgb24>());
    image.Mutate(ctx => ctx
        .Rotate(RotateMode.Rotate90)
        .Flip(FlipMode.Horizontal)
        .Flip(FlipMode.Vertical));

    using var ms = new MemoryStream();
    image.Save(ms, new JpegEncoder { Quality = 90 });
    return ms.ToArray();
}

static void SendShutdown(FileStream stream)
{
    SendCommand(stream, [.. Encoding.ASCII.GetBytes("CLE"), 0x00, 0x00, .. Encoding.ASCII.GetBytes("DC")]);
    SendCommand(stream, Encoding.ASCII.GetBytes("HAN"));
}

static void ReadLoop(string path, FileStream stream, CancellationToken token)
{
    // AKP153-family PCB's fixed physical-wiring-to-logical-grid mapping
    // (3x6=18 keys) - carries over here since the VID:PID match confirms
    // shared OEM hardware, but should be verified empirically against this
    // specific unit.
    int[] permutation = [4, 10, 16, 3, 9, 15, 2, 8, 14, 1, 7, 13, 0, 6, 12, 5, 11, 17];

    var buf = new byte[512];
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

            var logicalIndex = rawKey >= 1 && rawKey <= permutation.Length
                ? permutation[rawKey - 1]
                : -1;

            Console.WriteLine(
                $"[{Path.GetFileName(path)}] Key {logicalIndex} {(state != 0 ? "DOWN" : "UP")} (raw={rawKey})");
        }
    }
    catch (ObjectDisposedException) { }
    catch (IOException) { }
}
