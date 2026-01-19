using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace SonicEddy.Controls;

public class ValueSlider : Panel
{
    static ValueSlider()
    {
        AffectsRender<ValueSlider>(ValueProperty);
        AffectsRender<ValueSlider>(MinimumProperty);
        AffectsRender<ValueSlider>(MaximumProperty);
    }

    private readonly Rectangle _valueRect;

    public ValueSlider()
    {
        _valueRect = new Rectangle()
        {
            Fill = Brushes.BlanchedAlmond,
        };

        var stackPanel = new StackPanel()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var value = new TextBlock()
        {
            Foreground = Brushes.Black,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        stackPanel.Children.Add(value);

        value.Bind(TextBlock.TextProperty, new Binding(nameof(Value))
        {
            Source = this,
            Mode = BindingMode.OneWay,
            StringFormat = "F3"
        });

        Children.Add(_valueRect);
        Children.Add(stackPanel);

        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerMoved += OnPointerMoved;
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        CoerceValue(MinimumProperty);
        CoerceValue(MaximumProperty);
        CoerceValue(ValueProperty);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        base.ArrangeOverride(finalSize);

        var valueRectWidth =
            ((Value - Minimum) / (Maximum - Minimum)) * finalSize.Width;
        var valueRectHeight = finalSize.Height;
        _valueRect.Arrange(new(new(valueRectWidth, valueRectHeight)));

        return finalSize;
    }

    private bool _setValueOperation;
    private Point? _lastPoint;

    private void OnPointerPressed(object? sender,
        PointerPressedEventArgs eventArgs)
    {
        var point = eventArgs.GetCurrentPoint(this);

        if (point.Properties.IsLeftButtonPressed)
        {
            _lastPoint = point.Position;
            _setValueOperation = true;
            point.Pointer.Capture(this);
            Cursor = new Cursor(StandardCursorType.Hand);
        }
    }

    private void OnPointerReleased(object? sender,
        PointerReleasedEventArgs eventArgs)
    {
        _setValueOperation = false;
        _lastPoint = null;
        eventArgs.Pointer.Capture(null);
        Cursor = new Cursor(StandardCursorType.Arrow);
    }

    private const double Resolution = 600;

    private void OnPointerMoved(object? sender, PointerEventArgs eventArgs)
    {
        if (!_setValueOperation || _lastPoint is null) return;

        var position = eventArgs.GetPosition(this);
        var deltaY = _lastPoint.Value.Y - position.Y;
        var valueDelta = (deltaY / Resolution) * Maximum;
        Value += valueDelta;
        _lastPoint = position;
    }

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ValueSlider, double>(
            nameof(Value),
            defaultValue: 0.0,
            coerce: CoerceValue);

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

    public static readonly DirectProperty<ValueSlider, double>
        SliderWidthProperty =
            AvaloniaProperty.RegisterDirect<ValueSlider, double>(
                nameof(SliderWidth), o => o.SliderWidth);

    public double SliderWidth
    {
        get;
        set => SetAndRaise(SliderWidthProperty, ref field, value);
    }

    private static double CoerceValue(AvaloniaObject sender, double value)
    {
        var control = (ValueSlider)sender;
        return Math.Clamp(value, control.Minimum, control.Maximum);
    }
}