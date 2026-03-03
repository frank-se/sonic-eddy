using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace SonicEddy.Controls.MixerControls;

public partial class PanAndVolumeSectionWithMonitor : UserControl
{
    public PanAndVolumeSectionWithMonitor()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<float> LeftAverageProperty =
        AvaloniaProperty.Register<PanAndVolumeSectionWithMonitor, float>(
            nameof(LeftAverage));

    public float LeftAverage
    {
        get => GetValue(LeftAverageProperty);
        set => SetValue(LeftAverageProperty, value);
    }

    public static readonly StyledProperty<float> RightAverageProperty =
        AvaloniaProperty.Register<PanAndVolumeSectionWithMonitor, float>(
            nameof(RightAverage));

    public float RightAverage
    {
        get => GetValue(RightAverageProperty);
        set => SetValue(RightAverageProperty, value);
    }

    public static readonly StyledProperty<float> LeftPeakProperty =
        AvaloniaProperty.Register<PanAndVolumeSectionWithMonitor, float>(
            nameof(LeftPeak));

    public float LeftPeak
    {
        get => GetValue(LeftPeakProperty);
        set => SetValue(LeftPeakProperty, value);
    }

    public static readonly StyledProperty<float> RightPeakProperty =
        AvaloniaProperty.Register<PanAndVolumeSectionWithMonitor, float>(
            nameof(RightPeak));

    public float RightPeak
    {
        get => GetValue(RightPeakProperty);
        set => SetValue(RightPeakProperty, value);
    }
    
    public static readonly StyledProperty<double> VolumeProperty =
        AvaloniaProperty.Register<PanAndVolumeSectionWithMonitor, double>(
            nameof(Volume),
            defaultBindingMode: BindingMode.TwoWay);

    public double Volume
    {
        get => GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    public static readonly StyledProperty<double> PanProperty =
        AvaloniaProperty.Register<PanAndVolumeSectionWithMonitor, double>(
            nameof(Pan),
            defaultBindingMode: BindingMode.TwoWay);

    public double Pan
    {
        get => GetValue(PanProperty);
        set => SetValue(PanProperty, value);
    }
}