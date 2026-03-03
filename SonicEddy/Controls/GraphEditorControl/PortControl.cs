using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;

namespace SonicEddy.Controls.GraphEditorControl;

public enum PortTextSide
{
    Left,
    Right
}

public enum PortType
{
    In,
    Out
}

public class PortControl : StackPanel, IDisposable
{
    private readonly GraphEditor _editor;
    private readonly GraphPort _port;
    private Rectangle? _portRect;

    public PortControl(PortType type, PortTextSide side, GraphPort port,
        GraphEditor editor)
    {
        _port = port;
        _editor = editor;
        Orientation = Orientation.Horizontal;

        if (side == PortTextSide.Left)
        {
            AddText(port.Name);
            AddRectangle(type);
        }
        else if (side == PortTextSide.Right)
        {
            AddRectangle(type);
            AddText(port.Name);
        }
    }

    public Point GetConnectionCenterPoint(Control origin)
    {
        if (_portRect is null)
            throw new ApplicationException(
                "Port rect is not set for port control");

        var matrix = _portRect.TransformToVisual(origin);
        if (matrix is null)
            return default;
        var midPoint = new Point(_portRect.Bounds.Width / 2,
            _portRect.Bounds.Height / 2);
        return matrix.Value.Transform(midPoint);
    }

    public void SetHighlight()
    {
        _portRect?.Fill = Brushes.LawnGreen;
    }

    public void RemoveHighlight()
    {
        _portRect?.Fill = Brushes.DarkGreen;
    }

    private void AddText(string text)
    {
        Children.Add(
            new TextBlock()
            {
                Text = text,
                Margin = new(6, 4, 4, 4),
                Foreground = Brushes.Black,
            }
        );
    }

    private void AddRectangle(PortType type)
    {
        if (type == PortType.In)
        {
            _portRect = new()
            {
                Fill = Brushes.DarkGreen,
                Width = 10,
                Height = 10,
                Margin = new(4)
            };
            Children.Add(_portRect);
        }
        else if (type == PortType.Out)
        {
            _portRect = new()
            {
                Fill = Brushes.DarkGoldenrod,
                Width = 10,
                Height = 10,
                Margin = new(4)
            };

            _portRect.PointerPressed += OnOutPortRectanglePressed;
            Children.Add(_portRect);
        }
    }

    private void OnOutPortRectanglePressed(object? sender,
        PointerPressedEventArgs eventArgs)
    {
        if (sender is not Control rectangle) return;

        _editor.StartConnectionOperation(_port, this, eventArgs);
    }

    public void Dispose()
    {
        // TODO release managed resources here
    }
}