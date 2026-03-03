using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SonicEddy.Controls.MixerControls;

public partial class ChannelMonitorSection : UserControl
{
    public ChannelMonitorSection()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<float> LeftAverageProperty =
        AvaloniaProperty.Register<ChannelMonitorSection, float>(
            nameof(LeftAverage));

    public float LeftAverage
    {
        get => GetValue(LeftAverageProperty);
        set => SetValue(LeftAverageProperty, value);
    }

    public static readonly StyledProperty<float> RightAverageProperty =
        AvaloniaProperty.Register<ChannelMonitorSection, float>(
            nameof(RightAverage));

    public float RightAverage
    {
        get => GetValue(RightAverageProperty);
        set => SetValue(RightAverageProperty, value);
    }

    public static readonly StyledProperty<float> LeftPeakProperty =
        AvaloniaProperty.Register<ChannelMonitorSection, float>(
            nameof(LeftPeak));

    public float LeftPeak
    {
        get => GetValue(LeftPeakProperty);
        set => SetValue(LeftPeakProperty, value);
    }

    public static readonly StyledProperty<float> RightPeakProperty =
        AvaloniaProperty.Register<ChannelMonitorSection, float>(
            nameof(RightPeak));

    public float RightPeak
    {
        get => GetValue(RightPeakProperty);
        set => SetValue(RightPeakProperty, value);
    }
}