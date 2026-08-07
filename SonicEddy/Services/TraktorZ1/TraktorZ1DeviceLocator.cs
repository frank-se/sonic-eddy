using System;
using System.IO;
using System.Linq;

namespace SonicEddy.Services.TraktorZ1;

/// <summary>
/// Finds the current /dev/hidrawN node for a NI Traktor Kontrol Z1 by
/// matching its USB vendor/product ID in sysfs, instead of relying on a
/// hardcoded device path (which shifts across reconnects/reboots).
/// </summary>
public static class TraktorZ1DeviceLocator
{
    private const string HidrawRoot = "/sys/class/hidraw";
    private const string VendorId   = "17CC"; // Native Instruments
    private const string ProductId  = "1210"; // Traktor Kontrol Z1

    public static string? FindHidrawPath()
    {
        if (!Directory.Exists(HidrawRoot)) return null;

        foreach (var dir in Directory.EnumerateDirectories(HidrawRoot))
        {
            if (!TryReadHidId(Path.Combine(dir, "device", "uevent"),
                    out var vendor, out var product))
                continue;

            if (vendor.Equals(VendorId, StringComparison.OrdinalIgnoreCase) &&
                product.Equals(ProductId, StringComparison.OrdinalIgnoreCase))
                return $"/dev/{Path.GetFileName(dir)}";
        }

        return null;
    }

    private static bool TryReadHidId(string uEventPath, out string vendor,
        out string product)
    {
        vendor = product = "";
        if (!File.Exists(uEventPath)) return false;

        string[] lines;
        try
        {
            lines = File.ReadAllLines(uEventPath);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        // Format: HID_ID=<bus>:<vendor>:<product>, each hex, vendor/product
        // zero-padded to 8 digits, e.g. HID_ID=0003:000017CC:00001210
        var line = lines.FirstOrDefault(l =>
            l.StartsWith("HID_ID=", StringComparison.Ordinal));
        if (line is null) return false;

        var parts = line["HID_ID=".Length..].Split(':');
        if (parts.Length != 3) return false;

        vendor  = parts[1].TrimStart('0');
        product = parts[2].TrimStart('0');
        if (vendor.Length  == 0) vendor  = "0";
        if (product.Length == 0) product = "0";
        return true;
    }
}
