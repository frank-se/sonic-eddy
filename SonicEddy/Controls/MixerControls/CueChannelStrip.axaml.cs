using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace SonicEddy.Controls.MixerControls;

public partial class CueChannelStrip : UserControl
{
    public static readonly StyledProperty<double> VolumeProperty =
        AvaloniaProperty.Register<CueChannelStrip, double>(
            nameof(Volume),
            defaultBindingMode: BindingMode.TwoWay);

    public double Volume
    {
        get => GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    public static readonly StyledProperty<double> PanProperty =
        AvaloniaProperty.Register<CueChannelStrip, double>(
            nameof(Pan),
            defaultBindingMode: BindingMode.TwoWay);

    public double Pan
    {
        get => GetValue(PanProperty);
        set => SetValue(PanProperty, value);
    }

    public static readonly StyledProperty<float> LeftAverageProperty =
        AvaloniaProperty.Register<CueChannelStrip, float>(nameof(LeftAverage));

    public float LeftAverage
    {
        get => GetValue(LeftAverageProperty);
        set => SetValue(LeftAverageProperty, value);
    }

    public static readonly StyledProperty<float> RightAverageProperty =
        AvaloniaProperty.Register<CueChannelStrip, float>(nameof(RightAverage));

    public float RightAverage
    {
        get => GetValue(RightAverageProperty);
        set => SetValue(RightAverageProperty, value);
    }

    public static readonly StyledProperty<float> LeftPeakProperty =
        AvaloniaProperty.Register<CueChannelStrip, float>(nameof(LeftPeak));

    public float LeftPeak
    {
        get => GetValue(LeftPeakProperty);
        set => SetValue(LeftPeakProperty, value);
    }

    public static readonly StyledProperty<float> RightPeakProperty =
        AvaloniaProperty.Register<CueChannelStrip, float>(nameof(RightPeak));

    public float RightPeak
    {
        get => GetValue(RightPeakProperty);
        set => SetValue(RightPeakProperty, value);
    }

    public static readonly StyledProperty<ObservableCollection<IRoutingTarget>?>
        AudioToRoutingTargetsProperty =
            AvaloniaProperty.Register<CueChannelStrip, ObservableCollection<IRoutingTarget>?>(
                nameof(AudioToRoutingTargets));

    public ObservableCollection<IRoutingTarget>? AudioToRoutingTargets
    {
        get => GetValue(AudioToRoutingTargetsProperty);
        set => SetValue(AudioToRoutingTargetsProperty, value);
    }

    public static readonly StyledProperty<IRoutingTarget?>
        SelectedAudioToRoutingTargetProperty =
            AvaloniaProperty.Register<CueChannelStrip, IRoutingTarget?>(
                nameof(SelectedAudioToRoutingTarget),
                defaultBindingMode: BindingMode.TwoWay);

    public IRoutingTarget? SelectedAudioToRoutingTarget
    {
        get => GetValue(SelectedAudioToRoutingTargetProperty);
        set => SetValue(SelectedAudioToRoutingTargetProperty, value);
    }

    public CueChannelStrip()
    {
        InitializeComponent();
    }
}
