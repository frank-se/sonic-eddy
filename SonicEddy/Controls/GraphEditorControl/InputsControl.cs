using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace SonicEddy.Controls.GraphEditorControl;

public class InputsControl : StackPanel
{
    private readonly GraphEditorCanvas _canvas;
    private readonly GraphNode _node;
    private readonly TextBlock _header;

    private NodeDragDropState _dragState = NodeDragDropState.None;
    private Point? _lastPointerPosition;

    public InputsControl(GraphNode node, GraphEditorCanvas canvas)
    {
        _canvas = canvas;
        _node = node;
        Orientation = Orientation.Vertical;
        Background = Brushes.Bisque;

        _header = new TextBlock
        {
            Text = node.Name,
            Margin = new Thickness(6, 4, 4, 4),
            Foreground = Brushes.Black,
            FontWeight = FontWeight.Bold
        };
        Children.Add(_header);

        _header.PointerPressed += OnHeaderPressed;
        _header.PointerMoved += OnHeaderMoved;
        _header.PointerReleased += OnHeaderReleased;

        foreach (var port in node.OutPorts)
        {
            var control = new PortControl(PortType.Out, PortTextSide.Left, port, canvas);
            Children.Add(control);
            canvas.SetControl(port, control);
            GraphEditorCanvas.SetPort(control, port);
        }
    }

    private void OnHeaderPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not TextBlock tb) return;
        _dragState = NodeDragDropState.MoveNode;
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
        _canvas.UpdateCanvasSize();
    }
}
