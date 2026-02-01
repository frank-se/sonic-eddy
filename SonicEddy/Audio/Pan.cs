using System;
using System.Linq;

namespace SonicEddy.Audio;

public static class Pan
{
    public static (double Pan, double Volume) GetPanAndVolumeFromGains(
        double leftGain,
        double rightGain)
    {
        var volume = Math.Sqrt(leftGain * leftGain + rightGain * rightGain);
        var pan = Math.Atan2(rightGain, leftGain) / (Math.PI / 2.0);
        return new()
        {
            Pan = pan * 2 - 1,
            Volume = volume
        };
    }

    public static double[] GetGainsFromPanAndVolume(double pan, double volume)
    {
        var angle = (pan + 1.0) * (Math.PI / 4.0);
        var left = Math.Cos(angle);
        var right = Math.Sin(angle);
        return [volume * left, volume * right];
    }

    private static readonly double Boost = Math.Sqrt(2);

    public static double[] BoostToExternal(this double[] internalGains) =>
        internalGains.Select(g => g * Boost).ToArray();


    public static double[] AttenuateFromExternal(this double[] externalGains) =>
        externalGains.Select(g => g / Boost).ToArray();
}