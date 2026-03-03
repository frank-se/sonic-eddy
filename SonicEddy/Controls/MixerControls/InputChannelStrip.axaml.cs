using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SonicEddy.Controls.MixerControls;

public partial class InputChannelStrip : UserControl
{
    /*
     * Channel header
     */
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<InputChannelStrip, string>(
            nameof(Text),
            defaultValue: string.Empty);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<InputChannelStrip, bool>(nameof(IsSelected));

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<InputChannelStrip, ICommand?>(
            nameof(SelectCommand));

    public ICommand? SelectCommand
    {
        get => GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    public static readonly StyledProperty<object?>
        SelectCommandParameterProperty =
            AvaloniaProperty.Register<InputChannelStrip, object?>(
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
        AvaloniaProperty.Register<InputChannelStrip, float>(nameof(Volume));

    public float Volume
    {
        get => GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    public static readonly StyledProperty<float> PanProperty =
        AvaloniaProperty.Register<InputChannelStrip, float>(nameof(Pan));

    public float Pan
    {
        get => GetValue(PanProperty);
        set => SetValue(PanProperty, value);
    }

    public static readonly StyledProperty<float> LeftAverageProperty =
        AvaloniaProperty.Register<InputChannelStrip, float>(
            nameof(LeftAverage));

    public float LeftAverage
    {
        get => GetValue(LeftAverageProperty);
        set => SetValue(LeftAverageProperty, value);
    }

    public static readonly StyledProperty<float> RightAverageProperty =
        AvaloniaProperty.Register<InputChannelStrip, float>(
            nameof(RightAverage));

    public float RightAverage
    {
        get => GetValue(RightAverageProperty);
        set => SetValue(RightAverageProperty, value);
    }

    public static readonly StyledProperty<float> LeftPeakProperty =
        AvaloniaProperty.Register<InputChannelStrip, float>(
            nameof(LeftPeak));

    public float LeftPeak
    {
        get => GetValue(LeftPeakProperty);
        set => SetValue(LeftPeakProperty, value);
    }

    public static readonly StyledProperty<float> RightPeakProperty =
        AvaloniaProperty.Register<InputChannelStrip, float>(
            nameof(RightPeak));

    public float RightPeak
    {
        get => GetValue(RightPeakProperty);
        set => SetValue(RightPeakProperty, value);
    }
    
    public InputChannelStrip()
    {
        InitializeComponent();
    }
}