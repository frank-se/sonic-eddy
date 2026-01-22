using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace SonicEddy.Controls;

public class PanSlider : Panel
{
    static PanSlider()
    {
        AffectsArrange<PanSlider>(ValueProperty);
    }

    private readonly Rectangle _leftValueRect;
    private readonly Rectangle _rightValueRect;

    public PanSlider()
    {
        _leftValueRect = new()
        {
            Fill = Brushes.BlanchedAlmond,
        };

        _rightValueRect = new()
        {
            Fill = Brushes.Aquamarine,
        };

        var stackPanel = new StackPanel()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var text = new TextBlock()
        {
            Foreground = Brushes.Black,
            TextAlignment = TextAlignment.Center,
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

        stackPanel.Children.Add(text);
        stackPanel.Children.Add(value);

        text.Bind(TextBlock.TextProperty, new Binding(nameof(Text))
        {
            Source = this,
            Mode = BindingMode.OneWay
        });

        value.Bind(TextBlock.TextProperty, new Binding(nameof(Value))
        {
            Source = this,
            Mode = BindingMode.OneWay,
            StringFormat = "F3"
        });

        Children.Add(_leftValueRect);
        Children.Add(_rightValueRect);
        Children.Add(stackPanel);

        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerMoved += OnPointerMoved;
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        CoerceValue(MaximumProperty);
        CoerceValue(ValueProperty);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        base.ArrangeOverride(finalSize);

        var halfWidth = Bounds.Width / 2;
        if (Value >= 0)
        {
            var right = new Rect(halfWidth, 0, halfWidth * (Value / Maximum),
                Bounds.Height);
            var left = new Rect(halfWidth, 0, 0, 0);
            _rightValueRect.Arrange(right);
            _leftValueRect.Arrange(left);
        }
        else
        {
            var right = new Rect(halfWidth, 0, 0, Bounds.Height);

            var barLength = (-1 * Value / Maximum) * halfWidth;
            var x = halfWidth - barLength;
            var left = new Rect(x, 0, barLength, Bounds.Height);
            
            _rightValueRect.Arrange(right);
            _leftValueRect.Arrange(left);
        }

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

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<PanSlider, string>(
            nameof(Text),
            defaultValue: string.Empty);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<PanSlider, double>(
            nameof(Value),
            defaultValue: 0.0,
            coerce: CoerceValue);

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<PanSlider, double>(
            nameof(Maximum),
            defaultValue: 1.0);

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    private static double CoerceValue(AvaloniaObject sender, double value)
    {
        var control = (PanSlider)sender;
        return Math.Clamp(value, -control.Maximum, control.Maximum);
    }
}