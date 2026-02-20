using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;

namespace SonicEddy.Controls.MixerControls;

public class PanAndVolumeSection : Grid
{
    public static readonly StyledProperty<double> VolumeProperty =
        AvaloniaProperty.Register<PanAndVolumeSection, double>(
            nameof(Volume),
            defaultBindingMode: BindingMode.TwoWay);

    public double Volume
    {
        get => GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    public static readonly StyledProperty<double> PanProperty =
        AvaloniaProperty.Register<PanAndVolumeSection, double>(
            nameof(Pan),
            defaultBindingMode: BindingMode.TwoWay);

    public double Pan
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

        Margin = new Thickness(6);

        _volumeSlider = new()
        {
            Text = "Volume",
            IsVertical = true,
            Background = Brushes.Gray,
            Margin = new Thickness(2, 4, 2, 2),
            Maximum = 1,
            Minimum = 0
        };

        _volumeSlider.Bind(ValueSlider.ValueProperty, new Binding("Volume")
        {
            Source = this,
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

        _panSlider.Bind(PanSlider.ValueProperty, new Binding("Pan")
        {
            Source = this,
            Mode = BindingMode.TwoWay
        });

        SetRow(_panSlider, 1);
        SetColumn(_panSlider, 0);

        Children.Add(_volumeSlider);
        Children.Add(_panSlider);
    }
}