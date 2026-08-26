using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SonicEddy.Services.SoomfonDeck;

/// <summary>
/// Finds the current /dev/hidrawN node(s) for a Soomfon XF-CN001 stream
/// deck by matching its USB vendor/product ID in sysfs - same idiom as
/// <see cref="TraktorZ1.TraktorZ1DeviceLocator"/>. Unlike the Z1, this
/// device exposes two matching HID interfaces at once (confirmed
/// empirically: both accept writes, only one produces real input reports)
/// so this returns every match rather than the first.
/// </summary>
public static class SoomfonDeckLocator
{
    private const string HidrawRoot = "/sys/class/hidraw";
    private const string VendorId   = "1500";
    private const string ProductId  = "3003";

    public static IReadOnlyList<string> FindHidrawPaths()
    {
        if (!Directory.Exists(HidrawRoot)) return [];

        var paths = new List<string>();
        foreach (var dir in Directory.EnumerateDirectories(HidrawRoot))
        {
            if (!TryReadHidId(Path.Combine(dir, "device", "uevent"),
                    out var vendor, out var product))
                continue;

            if (vendor.Equals(VendorId, StringComparison.OrdinalIgnoreCase) &&
                product.Equals(ProductId, StringComparison.OrdinalIgnoreCase))
                paths.Add($"/dev/{Path.GetFileName(dir)}");
        }

        return paths;
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
        // zero-padded to 8 digits, e.g. HID_ID=0003:00001500:00003003
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
