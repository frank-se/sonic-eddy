using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace SonicEddy.Controls.MixerControls;

public class ChannelMonitorValue : Panel
{
    static ChannelMonitorValue()
    {
        AffectsArrange<ChannelMonitorValue>(ValueProperty);
    }

    public ChannelMonitorValue()
    {
        _valueRect = new()
        {
            Fill = Brushes.DarkGreen,
        };
        
        Children.Add(_valueRect);
    }

    private readonly Rectangle _valueRect;

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ValueSlider, double>(
            nameof(Value));

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<ValueSlider, double>(nameof(Maximum));

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<ValueSlider, double>(nameof(Minimum));

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set
        {
            if (Minimum > Value)
            {
                Value = Minimum;
            }

            SetValue(MinimumProperty, value);
        }
    }

    public static readonly StyledProperty<bool> IsVerticalProperty =
        AvaloniaProperty.Register<ValueSlider, bool>(
            nameof(IsVertical),
            defaultValue: false);

    public bool IsVertical
    {
        get => GetValue(IsVerticalProperty);
        set => SetValue(IsVerticalProperty, value);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        base.ArrangeOverride(finalSize);

        if (IsVertical)
        {
            var valueRectHeight =
                ((Value - Minimum) / (Maximum - Minimum)) * finalSize.Height;
            var y = finalSize.Height - valueRectHeight;

            var valueRectWidth = finalSize.Width;

            var rect = new Rect(0, y, valueRectWidth, valueRectHeight);

            if (!rect.IsInvalidRect())
                _valueRect.Arrange(rect);
        }
        else
        {
            var valueRectWidth =
                ((Value - Minimum) / (Maximum - Minimum)) * finalSize.Width;
            var valueRectHeight = finalSize.Height;

            var rect = new Rect((new(valueRectWidth, valueRectHeight)));

            if (!rect.IsInvalidRect())
                _valueRect.Arrange(rect);
        }

        return finalSize;
    }
}