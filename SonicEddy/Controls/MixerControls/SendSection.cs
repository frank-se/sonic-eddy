using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;

namespace SonicEddy.Controls.MixerControls;

public class SendSection : Grid
{
    public static readonly StyledProperty<float> Send1TrimProperty =
        AvaloniaProperty
            .Register<SendSection, float>(
                nameof(Send1Trim),
                defaultBindingMode: BindingMode.TwoWay);

    public float Send1Trim
    {
        get => GetValue(Send1TrimProperty);
        set => SetValue(Send1TrimProperty, value);
    }

    public static readonly StyledProperty<float> Send2TrimProperty =
        AvaloniaProperty
            .Register<SendSection, float>(
                nameof(Send2Trim),
                defaultBindingMode: BindingMode.TwoWay);

    public float Send2Trim
    {
        get => GetValue(Send2TrimProperty);
        set => SetValue(Send2TrimProperty, value);
    }

    public static readonly StyledProperty<float> Send3TrimProperty =
        AvaloniaProperty
            .Register<SendSection, float>(
                nameof(Send3Trim),
                defaultBindingMode: BindingMode.TwoWay);

    public float Send3Trim
    {
        get => GetValue(Send3TrimProperty);
        set => SetValue(Send3TrimProperty, value);
    }

    public static readonly StyledProperty<float> Send4TrimProperty =
        AvaloniaProperty
            .Register<SendSection, float>(
                nameof(Send4Trim),
                defaultBindingMode: BindingMode.TwoWay);

    public float Send4Trim
    {
        get => GetValue(Send4TrimProperty);
        set => SetValue(Send4TrimProperty, value);
    }

    public SendSection()
    {
        ColumnDefinitions = ColumnDefinitions.Parse("*,*");
        RowDefinitions = RowDefinitions.Parse("20,40,40");

        var header = new TextBlock()
        {
            FontSize = 12,
            Text = "Sends",
            Margin = new Thickness(6)
        };

        SetRow(header, 0);
        SetColumn(header, 0);
        SetColumnSpan(header, 2);

        Children.Add(header);

        ValueSlider[] sliders =
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

        SetRow(sliders[0], 1);
        SetColumn(sliders[0], 0);

        SetRow(sliders[1], 1);
        SetColumn(sliders[1], 1);

        SetRow(sliders[2], 2);
        SetColumn(sliders[2], 0);

        SetRow(sliders[3], 2);
        SetColumn(sliders[3], 1);

        sliders[0].Bind(ValueSlider.ValueProperty, new Binding("Send1Trim")
        {
            Source = this,
            Mode = BindingMode.TwoWay
        });

        sliders[1].Bind(ValueSlider.ValueProperty, new Binding("Send2Trim")
        {
            Source = this,
            Mode = BindingMode.TwoWay
        });

        sliders[2].Bind(ValueSlider.ValueProperty, new Binding("Send3Trim")
        {
            Source = this,
            Mode = BindingMode.TwoWay
        });

        sliders[3].Bind(ValueSlider.ValueProperty, new Binding("Send4Trim")
        {
            Source = this,
            Mode = BindingMode.TwoWay
        });

        Children.AddRange(sliders);
    }
}