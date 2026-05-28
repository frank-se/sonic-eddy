using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace SonicEddy.Controls.GraphEditorControl;

public class NodeControl : StackPanel, IDisposable
{
    private readonly GraphEditorCanvas _canvas;
    private readonly TextBlock _nameTextBlock;
    private readonly GraphNode _node;

    public NodeControl(GraphNode node, GraphEditorCanvas canvas)
    {
        _canvas = canvas;
        _node = node;
        Orientation = Orientation.Vertical;
        Background = Brushes.Bisque;

        _nameTextBlock = new TextBlock
        {
            Text = node.Name,
            Margin = new Thickness(6, 4, 4, 4),
            Foreground = Brushes.Black,
            FontWeight = FontWeight.Bold
        };
        Children.Add(_nameTextBlock);

        _nameTextBlock.PointerPressed += OnHeaderPressed;
        _nameTextBlock.PointerMoved += OnHeaderMoved;
        _nameTextBlock.PointerReleased += OnHeaderReleased;

        var pos = canvas.NextNodePosition();
        Canvas.SetLeft(this, pos.X);
        Canvas.SetTop(this, pos.Y);

        var portRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
        var inCol = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(4) };
        var outCol = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(4) };

        foreach (var port in node.InPorts)
        {
            var control = new PortControl(PortType.In, PortTextSide.Right, port, canvas);
            inCol.Children.Add(control);
            canvas.SetControl(port, control);
            GraphEditorCanvas.SetPort(control, port);
        }

        foreach (var port in node.OutPorts)
        {
            var control = new PortControl(PortType.Out, PortTextSide.Left, port, canvas);
            outCol.Children.Add(control);
            canvas.SetControl(port, control);
            GraphEditorCanvas.SetPort(control, port);
        }

        portRow.Children.Add(inCol);
        portRow.Children.Add(outCol);
        Children.Add(portRow);
    }

    private NodeDragDropState _dragState = NodeDragDropState.None;
    private Point? _lastPointerPosition;
    private bool _hasDragged;

    private void OnHeaderPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not TextBlock tb) return;
        _dragState = NodeDragDropState.MoveNode;
        _hasDragged = false;
        e.Pointer.Capture(tb);
        Cursor = new Cursor(StandardCursorType.DragMove);
        _lastPointerPosition = e.GetPosition(null);
    }

    private void OnHeaderMoved(object? sender, PointerEventArgs e)
    {
        if (_dragState != NodeDragDropState.MoveNode || _lastPointerPosition is null) return;

        var current = e.GetPosition(null);
        var diffX = _lastPointerPosition.Value.X - current.X;
        var diffY = _lastPointerPosition.Value.Y - current.Y;

        if (Math.Abs(diffX) > 2 || Math.Abs(diffY) > 2)
            _hasDragged = true;

        _lastPointerPosition = current;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Canvas.SetLeft(this, Canvas.GetLeft(this) - diffX);
            Canvas.SetTop(this, Canvas.GetTop(this) - diffY);
            _canvas.UpdateConnectionsForNodeMove(_node);
        });
    }

    private void OnHeaderReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragState = NodeDragDropState.None;
        e.Pointer.Capture(null);
        Cursor = new Cursor(StandardCursorType.Arrow);
        if (!_hasDragged) _canvas.SelectNode(_node);
        _canvas.UpdateCanvasSize();
    }

    public void Dispose()
    {
        _nameTextBlock.PointerPressed -= OnHeaderPressed;
        _nameTextBlock.PointerMoved -= OnHeaderMoved;
        _nameTextBlock.PointerReleased -= OnHeaderReleased;
    }
}
