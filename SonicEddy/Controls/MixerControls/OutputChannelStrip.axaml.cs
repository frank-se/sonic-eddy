using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace SonicEddy.Controls.MixerControls;

public partial class OutputChannelStrip : UserControl
{
    /*
     * Channel header
     */
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<OutputChannelStrip, string>(
            nameof(Text),
            defaultValue: string.Empty);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<OutputChannelStrip, bool>(nameof(IsSelected));

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<OutputChannelStrip, ICommand?>(
            nameof(SelectCommand));

    public ICommand? SelectCommand
    {
        get => GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    public static readonly StyledProperty<object?>
        SelectCommandParameterProperty =
            AvaloniaProperty.Register<OutputChannelStrip, object?>(
                nameof(SelectCommandParameter));

    public object? SelectCommandParameter
    {
        get => GetValue(SelectCommandParameterProperty);
        set => SetValue(SelectCommandParameterProperty, value);
    }

    /*
     * Volume section
     */
    public static readonly StyledProperty<float> VolumeProperty =
        AvaloniaProperty.Register<OutputChannelStrip, float>(nameof(Volume));

    public float Volume
    {
        get => GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    public static readonly StyledProperty<float> PanProperty =
        AvaloniaProperty.Register<OutputChannelStrip, float>(nameof(Pan));

    public float Pan
    {
        get => GetValue(PanProperty);
        set => SetValue(PanProperty, value);
    }

    public static readonly StyledProperty<float> LeftAverageProperty =
        AvaloniaProperty.Register<OutputChannelStrip, float>(
            nameof(LeftAverage));

    public float LeftAverage
    {
        get => GetValue(LeftAverageProperty);
        set => SetValue(LeftAverageProperty, value);
    }

    public static readonly StyledProperty<float> RightAverageProperty =
        AvaloniaProperty.Register<OutputChannelStrip, float>(
            nameof(RightAverage));

    public float RightAverage
    {
        get => GetValue(RightAverageProperty);
        set => SetValue(RightAverageProperty, value);
    }

    public static readonly StyledProperty<float> LeftPeakProperty =
        AvaloniaProperty.Register<OutputChannelStrip, float>(
            nameof(LeftPeak));

    public float LeftPeak
    {
        get => GetValue(LeftPeakProperty);
        set => SetValue(LeftPeakProperty, value);
    }

    public static readonly StyledProperty<float> RightPeakProperty =
        AvaloniaProperty.Register<OutputChannelStrip, float>(
            nameof(RightPeak));

    public float RightPeak
    {
        get => GetValue(RightPeakProperty);
        set => SetValue(RightPeakProperty, value);
    }

    public OutputChannelStrip()
    {
        InitializeComponent();
    }
}