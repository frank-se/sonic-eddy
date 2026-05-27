using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;

namespace SonicEddy.Controls.GraphEditorControl;

public enum PortTextSide { Left, Right }

public enum PortType { In, Out }

public class PortControl : StackPanel, IDisposable
{
    private readonly GraphEditorCanvas _canvas;
    private readonly GraphPort _port;
    private Rectangle? _portRect;

    public PortControl(PortType type, PortTextSide side, GraphPort port,
        GraphEditorCanvas canvas)
    {
        _port = port;
        _canvas = canvas;
        Orientation = Orientation.Horizontal;

        if (side == PortTextSide.Left)
        {
            AddText(port.Name);
            AddRectangle(type);
        }
        else
        {
            AddRectangle(type);
            AddText(port.Name);
        }
    }

    public Point GetConnectionCenterPoint(Control origin)
    {
        if (_portRect is null)
            throw new ApplicationException("Port rect is not set for port control");

        var matrix = _portRect.TransformToVisual(origin);
        if (matrix is null) return default;
        return matrix.Value.Transform(
            new Point(_portRect.Bounds.Width / 2, _portRect.Bounds.Height / 2));
    }

    public void SetHighlight() => _portRect?.Fill = Brushes.LawnGreen;
    public void RemoveHighlight() => _portRect?.Fill = Brushes.DarkGreen;

    private void AddText(string text) =>
        Children.Add(new TextBlock
        {
            Text = text,
            Margin = new Thickness(6, 4, 4, 4),
            Foreground = Brushes.Black
        });

    private void AddRectangle(PortType type)
    {
        _portRect = new Rectangle
        {
            Fill = type == PortType.In ? Brushes.DarkGreen : Brushes.DarkGoldenrod,
            Width = 10,
            Height = 10,
            Margin = new Thickness(4)
        };

        if (type == PortType.Out)
            _portRect.PointerPressed += OnOutPortPressed;

        Children.Add(_portRect);
    }

    private void OnOutPortPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control) return;
        _canvas.StartConnectionOperation(_port, this, e);
    }

    public void Dispose() { }
}
