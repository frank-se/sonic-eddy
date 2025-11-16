using System;

namespace SonicEddy.Audio;

public static class Pan
{
    public static double GetPanFromGains(double leftGain, double rightGain)
    {
        var normalizationFactor = Math.Sqrt(
            leftGain * leftGain +
            rightGain * rightGain);

        var normalizedLeft = leftGain / normalizationFactor;
        var normalizedRight = rightGain / normalizationFactor;

        var angleRadians = Math.Atan2(normalizedRight, normalizedLeft);

        return 2.0 / Math.PI * angleRadians;
    }

    public static Tuple<double, double> GetGainsFromPanAndVolume(double pan,
        double volume)
    {
        var angle = Math.PI / 2.0 * pan;
        return new(Math.Cos(angle) * volume, Math.Sin(angle) * volume);
    }
}