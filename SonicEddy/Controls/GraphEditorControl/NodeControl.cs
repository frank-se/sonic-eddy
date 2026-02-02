using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Remote.Protocol.Input;

namespace SonicEddy.Controls.GraphEditorControl;

public class NodeControl : StackPanel, IDisposable
{
    private readonly TextBlock _nameTextBlock;

    private static double _nextX = 20.0;
    private static double _nextY = 20.0;
    
    public NodeControl(IGraphNode node) : base()
    {
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
            var button = new Button()
            {
                Content = inPort.Name,
                Background = Brushes.DarkGray,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new(4)
            };

            inPortStackPanel.Children.Add(button);
            inPort.Control = button;
        }

        foreach (var outPort in node.OutPorts)
        {
            var button = new Button()
            {
                Content = outPort.Name,
                Background = Brushes.DarkGray,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new(4)
            };

            outPortStackPanel.Children.Add(button);
            outPort.Control = button;
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