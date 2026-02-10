using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;

namespace SonicEddy.Controls.MixerControls;

public class PanAndVolumeSection : Grid
{
    public static readonly StyledProperty<float> VolumeProperty =
        AvaloniaProperty.Register<PanAndVolumeSection, float>(nameof(Volume));

    public float Volume
    {
        get => GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    public static readonly StyledProperty<float> PanProperty =
        AvaloniaProperty.Register<PanAndVolumeSection, float>(nameof(Pan));

    public float Pan
    {
        get => GetValue(PanProperty);
        set => SetValue(PanProperty, value);
    }

    private ValueSlider _volumeSlider;
    private PanSlider _panSlider;

    public PanAndVolumeSection()
    {
        ColumnDefinitions = ColumnDefinitions.Parse("*");
        RowDefinitions = RowDefinitions.Parse("3*,40");

        _volumeSlider = new()
        {
            Text = "Volume",
            IsVertical = true,
            Background = Brushes.Gray,
            Margin = new Thickness(2, 4, 2, 2),
            Maximum = 1,
            Minimum = 0
        };

        _volumeSlider.Bind(ValueSlider.ValueProperty, new Binding
        {
            Source = Volume,
            Mode = BindingMode.TwoWay
        });

        SetRow(_volumeSlider, 0);
        SetColumn(_volumeSlider, 0);

        _panSlider = new()
        {
            Text = "Pan",
            Background = Brushes.DimGray,
            Margin = new Thickness(2, 2, 2, 2),
        };

        _panSlider.Bind(PanSlider.ValueProperty, new Binding
        {
            Source = Volume,
            Mode = BindingMode.TwoWay
        });
        
        SetRow(_panSlider, 1);
        SetColumn(_panSlider, 0);

        Children.Add(_volumeSlider);
        Children.Add(_panSlider);
    }
}