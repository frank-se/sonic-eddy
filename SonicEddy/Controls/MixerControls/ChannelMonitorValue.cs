using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace SonicEddy.Controls.MixerControls;

public class ChannelMonitorValue : Panel
{
    static ChannelMonitorValue()
    {
        AffectsArrange<ChannelMonitorValue>(ValueProperty, PeakProperty);
    }

    public ChannelMonitorValue()
    {
        _valueRect = new()
        {
            Fill = Brushes.DarkGreen,
        };
        _peakRect = new()
        {
            Fill = Brushes.Red,
        };

        Children.Add(_valueRect);
        Children.Add(_peakRect);
    }

    private readonly Rectangle _valueRect;
    private readonly Rectangle _peakRect;

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ValueSlider, double>(
            nameof(Value));

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly StyledProperty<double> PeakProperty =
        AvaloniaProperty.Register<ValueSlider, double>(nameof(Peak));

    public double Peak
    {
        get => GetValue(PeakProperty);
        set => SetValue(PeakProperty, value);
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

    private const double PeakThickness = 2.0;

    protected override Size ArrangeOverride(Size finalSize)
    {
        base.ArrangeOverride(finalSize);

        var range = Maximum - Minimum;

        if (IsVertical)
        {
            var valueRectHeight = ((Value - Minimum) / range) * finalSize.Height;
            var y = finalSize.Height - valueRectHeight;
            var valueRect = new Rect(0, y, finalSize.Width, valueRectHeight);
            if (!valueRect.IsInvalidRect())
                _valueRect.Arrange(valueRect);

            var peakY = finalSize.Height - ((Peak - Minimum) / range) * finalSize.Height;
            var peakRect = new Rect(0, peakY, finalSize.Width, PeakThickness);
            if (!peakRect.IsInvalidRect())
                _peakRect.Arrange(peakRect);
        }
        else
        {
            var valueRectWidth = ((Value - Minimum) / range) * finalSize.Width;
            var valueRect = new Rect(new(valueRectWidth, finalSize.Height));
            if (!valueRect.IsInvalidRect())
                _valueRect.Arrange(valueRect);

            var peakX = ((Peak - Minimum) / range) * finalSize.Width;
            var peakRect = new Rect(peakX, 0, PeakThickness, finalSize.Height);
            if (!peakRect.IsInvalidRect())
                _peakRect.Arrange(peakRect);
        }

        return finalSize;
    }
}