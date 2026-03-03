namespace SonicEddy.Controls.MixerControls;

public interface IPanAndVolume
{
    double Volume { get; set; }
    double Pan { get; set; }
    float LeftPeak { get; }
    float RightPeak { get; }
    float LeftAverage { get; }
    float RightAverage { get; }
}