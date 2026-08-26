using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SonicEddy.Services.SoomfonDeck;

/// <summary>
/// Builds the flat solid-color key images the M/E switcher paints onto the
/// deck (red/green/yellow highlights, no icons/text - see the plan this was
/// built from). Only ever used for the 15 interactive keys, never the
/// display-strip indices (5/11/17), so always the "normal key" 95x95 size -
/// see get_image_format_for_key in the akp153-family plugin this protocol
/// was ported from.
/// </summary>
public static class SoomfonDeckImages
{
    public const int KeySize = 95;

    // Built once and reused - encoding a fresh JPEG on every repaint was
    // part of what made T-bar dragging unusably laggy (see
    // MixEffectsSwitcherViewModel), and these three colors are the only
    // ones this feature ever needs.
    private static readonly Lazy<byte[]> RedJpeg = new(() => BuildSolidColorJpeg(Color.Red));
    private static readonly Lazy<byte[]> GreenJpeg = new(() => BuildSolidColorJpeg(Color.Green));
    private static readonly Lazy<byte[]> YellowJpeg = new(() => BuildSolidColorJpeg(Color.Yellow));

    public static byte[] Red => RedJpeg.Value;
    public static byte[] Green => GreenJpeg.Value;
    public static byte[] Yellow => YellowJpeg.Value;

    // Matches the akp153-family plugin's own image pipeline for this
    // protocol version: rotate 90 (clockwise) then mirror both axes, then
    // JPEG-encode. Invisible on a flat solid color, but kept correct in
    // case a labeled/icon variant is ever needed.
    public static byte[] BuildSolidColorJpeg(Color color)
    {
        using var image = new Image<Rgb24>(KeySize, KeySize, color.ToPixel<Rgb24>());
        image.Mutate(ctx => ctx
            .Rotate(RotateMode.Rotate90)
            .Flip(FlipMode.Horizontal)
            .Flip(FlipMode.Vertical));

        using var ms = new MemoryStream();
        image.Save(ms, new JpegEncoder { Quality = 90 });
        return ms.ToArray();
    }
}
