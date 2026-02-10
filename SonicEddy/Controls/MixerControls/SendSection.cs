using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace SonicEddy.Controls.MixerControls;

public class SendSection : Grid
{
    public static readonly StyledProperty<float> Send1TrimProperty =
        AvaloniaProperty
            .Register<PanAndVolumeSection, float>(nameof(Send1Trim));

    public float Send1Trim
    {
        get => GetValue(Send1TrimProperty);
        set => SetValue(Send1TrimProperty, value);
    }

    public static readonly StyledProperty<float> Send2TrimProperty =
        AvaloniaProperty
            .Register<PanAndVolumeSection, float>(nameof(Send2Trim));

    public float Send2Trim
    {
        get => GetValue(Send2TrimProperty);
        set => SetValue(Send2TrimProperty, value);
    }

    public static readonly StyledProperty<float> Send3TrimProperty =
        AvaloniaProperty
            .Register<PanAndVolumeSection, float>(nameof(Send3Trim));

    public float Send3Trim
    {
        get => GetValue(Send3TrimProperty);
        set => SetValue(Send3TrimProperty, value);
    }

    public static readonly StyledProperty<float> Send4TrimProperty =
        AvaloniaProperty
            .Register<PanAndVolumeSection, float>(nameof(Send4Trim));

    public float Send4Trim
    {
        get => GetValue(Send4TrimProperty);
        set => SetValue(Send4TrimProperty, value);
    }

    private readonly Button _headerButton;
    private readonly ValueSlider[] _sliders;

    public SendSection()
    {
        ColumnDefinitions = ColumnDefinitions.Parse("*,*");
        RowDefinitions = RowDefinitions.Parse("40,40,40");

        _headerButton = new()
        {
            Foreground = Brushes.White,
            Content = "Sends",
            FontSize = 14,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            BorderThickness = new(0),
            CornerRadius = new CornerRadius(0),
            Background = Brushes.Black,
        };

        SetRow(_headerButton, 0);
        SetColumn(_headerButton, 0);
        SetColumnSpan(_headerButton, 2);

        Children.Add(_headerButton);

        _sliders =
        [
            new()
            {
                Background = Brushes.DimGray,
                Margin = new Thickness(2),
                Text = "Send 1",
                Minimum = 0.0f,
                Maximum = 1.0f
            },
            new()
            {
                Background = Brushes.DimGray,
                Margin = new Thickness(2),
                Text = "Send 2",
                Minimum = 0.0f,
                Maximum = 1.0f
            },
            new()
            {
                Background = Brushes.DimGray,
                Margin = new Thickness(2),
                Text = "Send 3",
                Minimum = 0.0f,
                Maximum = 1.0f
            },
            new()
            {
                Background = Brushes.DimGray,
                Margin = new Thickness(2),
                Text = "Send 4",
                Minimum = 0.0f,
                Maximum = 1.0f
            },
        ];

        SetRow(_sliders[0], 1);
        SetColumn(_sliders[0], 0);

        SetRow(_sliders[1], 1);
        SetColumn(_sliders[1], 1);

        SetRow(_sliders[2], 2);
        SetColumn(_sliders[2], 0);

        SetRow(_sliders[3], 2);
        SetColumn(_sliders[3], 1);

        _sliders[0].Bind(ValueSlider.ValueProperty, new Binding()
        {
            Source = Send1Trim,
            Mode = BindingMode.TwoWay
        });

        _sliders[1].Bind(ValueSlider.ValueProperty, new Binding()
        {
            Source = Send2Trim,
            Mode = BindingMode.TwoWay
        });

        _sliders[2].Bind(ValueSlider.ValueProperty, new Binding()
        {
            Source = Send3Trim,
            Mode = BindingMode.TwoWay
        });

        _sliders[3].Bind(ValueSlider.ValueProperty, new Binding()
        {
            Source = Send4Trim,
            Mode = BindingMode.TwoWay
        });
        
        Children.AddRange(_sliders);
    }
}