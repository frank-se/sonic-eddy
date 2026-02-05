using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace SonicEddy.Controls.GraphEditorControl;

public class NodeControl : StackPanel, IDisposable
{
    private readonly GraphEditor _editor;
    private readonly TextBlock _nameTextBlock;
    private readonly GraphNode _node;

    private static double _nextX = 20.0;
    private static double _nextY = 20.0;

    public NodeControl(GraphNode node, GraphEditor editor)
    {
        _editor = editor;
        _node = node;
        Orientation = Orientation.Vertical;
        _nameTextBlock = new()
        {
            Text = node.Name,
            Margin = new(6, 4, 4, 4),
            Foreground = Brushes.Black,
            FontWeight = FontWeight.Bold
        };

        Children.Add(_nameTextBlock);

        _nameTextBlock.PointerPressed += OnHeaderPressed;
        _nameTextBlock.PointerMoved += OnHeaderMoved;
        _nameTextBlock.PointerReleased += OnHeaderReleased;

        Background = Brushes.Bisque;

        Canvas.SetLeft(this, _nextX);
        Canvas.SetTop(this, _nextY);

        _nextX += 150.0;
        _nextY += 30;

        var portStackPanel = new StackPanel()
        {
            Orientation = Orientation.Horizontal,
            Margin = new(4)
        };

        Children.Add(portStackPanel);

        var inPortStackPanel = new StackPanel()
        {
            Orientation = Orientation.Vertical,
            Margin = new(4)
        };

        var outPortStackPanel = new StackPanel()
        {
            Orientation = Orientation.Vertical,
            Margin = new(4)
        };

        foreach (var inPort in node.InPorts)
        {
            var control = new PortControl(PortType.In, PortTextSide.Right,
                inPort, _editor);

            inPortStackPanel.Children.Add(control);

            editor.SetControl(inPort, control);
            GraphEditor.SetPort(control, inPort);
        }

        foreach (var outPort in node.OutPorts)
        {
            var control = new PortControl(PortType.Out, PortTextSide.Left,
                outPort, _editor);

            outPortStackPanel.Children.Add(control);

            editor.SetControl(outPort, control);
            GraphEditor.SetPort(control, outPort);
        }

        portStackPanel.Children.Add(inPortStackPanel);
        portStackPanel.Children.Add(outPortStackPanel);
    }

    private NodeDragDropState _dragDropState = NodeDragDropState.None;
    private Point? _lastPointerPosition;

    private void OnHeaderPressed(object? sender,
        PointerPressedEventArgs eventArgs)
    {
        if (sender is not TextBlock textBlock) return;
        _dragDropState = NodeDragDropState.MoveNode;
        eventArgs.Pointer.Capture(textBlock);
        Cursor = new(StandardCursorType.DragMove);
        _lastPointerPosition = eventArgs.GetPosition(null);
    }

    private void OnHeaderMoved(object? sender, PointerEventArgs eventArgs)
    {
        if (_dragDropState == NodeDragDropState.MoveNode)
        {
            if (_lastPointerPosition is null) return;
            var x = Canvas.GetLeft(this);
            var y = Canvas.GetTop(this);
            var currentPosition = eventArgs.GetPosition(null);
            var diffX = _lastPointerPosition.Value.X - currentPosition.X;
            var diffY = _lastPointerPosition.Value.Y - currentPosition.Y;
            _lastPointerPosition = currentPosition;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Canvas.SetLeft(this, x - diffX);
                Canvas.SetTop(this, y - diffY);
                _editor.UpdateConnectionsForNodeMove(_node);
            });
        }
    }

    private void OnHeaderReleased(object? sender,
        PointerReleasedEventArgs eventArgs)
    {
        _dragDropState = NodeDragDropState.None;
        eventArgs.Pointer.Capture(null);
        Cursor = new(StandardCursorType.Arrow);
    }

    public void Dispose()
    {
        _nameTextBlock.PointerPressed -= OnHeaderPressed;
        _nameTextBlock.PointerMoved -= OnHeaderMoved;
        _nameTextBlock.PointerReleased -= OnHeaderReleased;
    }
}