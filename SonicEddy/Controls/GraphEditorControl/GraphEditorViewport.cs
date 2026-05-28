using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace SonicEddy.Controls.GraphEditorControl;

public class GraphEditorViewport : UserControl, IDisposable
{
    private readonly GraphEditorCanvas _canvas;
    private readonly ScrollViewer _scrollViewer;

    private bool _isPanning;
    private Point _panStartPointer;
    private Vector _panStartOffset;

    public GraphEditorViewport()
    {
        _canvas = new GraphEditorCanvas();
        _canvas.CanvasUpdated += OnCanvasUpdated;

        _scrollViewer = new ScrollViewer
        {
            Content = _canvas,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        Content = _scrollViewer;

        _scrollViewer.PointerPressed += OnPointerPressed;
        _scrollViewer.PointerMoved += OnPointerMoved;
        _scrollViewer.PointerReleased += OnPointerReleased;
    }

    /* ── styled properties (mirror GraphEditorCanvas, forwarded on change) ── */

    public static readonly StyledProperty<ObservableCollection<GraphNode>?> GraphNodesProperty =
        AvaloniaProperty.Register<GraphEditorViewport, ObservableCollection<GraphNode>?>(nameof(GraphNodes));

    public ObservableCollection<GraphNode>? GraphNodes
    {
        get => GetValue(GraphNodesProperty);
        set => SetValue(GraphNodesProperty, value);
    }

    public static readonly StyledProperty<ObservableCollection<GraphEdge>?> GraphEdgesProperty =
        AvaloniaProperty.Register<GraphEditorViewport, ObservableCollection<GraphEdge>?>(nameof(GraphEdges));

    public ObservableCollection<GraphEdge>? GraphEdges
    {
        get => GetValue(GraphEdgesProperty);
        set => SetValue(GraphEdgesProperty, value);
    }

    public static readonly StyledProperty<GraphNode?> GraphInputsProperty =
        AvaloniaProperty.Register<GraphEditorViewport, GraphNode?>(nameof(GraphInputs));

    public GraphNode? GraphInputs
    {
        get => GetValue(GraphInputsProperty);
        set => SetValue(GraphInputsProperty, value);
    }

    public static readonly StyledProperty<GraphNode?> GraphOutputsProperty =
        AvaloniaProperty.Register<GraphEditorViewport, GraphNode?>(nameof(GraphOutputs));

    public GraphNode? GraphOutputs
    {
        get => GetValue(GraphOutputsProperty);
        set => SetValue(GraphOutputsProperty, value);
    }

    public static readonly StyledProperty<ICommand?> CreateEdgeCommandProperty =
        AvaloniaProperty.Register<GraphEditorViewport, ICommand?>(nameof(CreateEdgeCommand));

    public ICommand? CreateEdgeCommand
    {
        get => GetValue(CreateEdgeCommandProperty);
        set => SetValue(CreateEdgeCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand?> DeleteEdgeCommandProperty =
        AvaloniaProperty.Register<GraphEditorViewport, ICommand?>(nameof(DeleteEdgeCommand));

    public ICommand? DeleteEdgeCommand
    {
        get => GetValue(DeleteEdgeCommandProperty);
        set => SetValue(DeleteEdgeCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand?> NodeSelectedCommandProperty =
        AvaloniaProperty.Register<GraphEditorViewport, ICommand?>(nameof(NodeSelectedCommand));

    public ICommand? NodeSelectedCommand
    {
        get => GetValue(NodeSelectedCommandProperty);
        set => SetValue(NodeSelectedCommandProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == GraphNodesProperty)
            _canvas.GraphNodes = change.NewValue as ObservableCollection<GraphNode>;
        else if (change.Property == GraphEdgesProperty)
            _canvas.GraphEdges = change.NewValue as ObservableCollection<GraphEdge>;
        else if (change.Property == GraphInputsProperty)
            _canvas.GraphInputs = change.NewValue as GraphNode;
        else if (change.Property == GraphOutputsProperty)
            _canvas.GraphOutputs = change.NewValue as GraphNode;
        else if (change.Property == CreateEdgeCommandProperty)
            _canvas.CreateEdgeCommand = change.NewValue as ICommand;
        else if (change.Property == DeleteEdgeCommandProperty)
            _canvas.DeleteEdgeCommand = change.NewValue as ICommand;
        else if (change.Property == NodeSelectedCommandProperty)
            _canvas.NodeSelectedCommand = change.NewValue as ICommand;
    }

    /* ── canvas shift compensation ───────────────────────────────────────── */

    private void OnCanvasUpdated(object? sender, GraphEditorCanvas.CanvasUpdatedEventArgs e)
    {
        if (e.ShiftX == 0 && e.ShiftY == 0) return;
        _scrollViewer.Offset = new Vector(
            _scrollViewer.Offset.X + e.ShiftX,
            _scrollViewer.Offset.Y + e.ShiftY);
    }

    /* ── middle-button pan ────────────────────────────────────────────────── */

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(_scrollViewer);
        if (!point.Properties.IsMiddleButtonPressed) return;

        _isPanning = true;
        _panStartPointer = e.GetPosition(_scrollViewer);
        _panStartOffset = _scrollViewer.Offset;
        e.Pointer.Capture(_scrollViewer);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning) return;
        var current = e.GetPosition(_scrollViewer);
        var delta = _panStartPointer - current;
        _scrollViewer.Offset = new Vector(
            _panStartOffset.X + delta.X,
            _panStartOffset.Y + delta.Y);
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPanning) return;
        _isPanning = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /* ── IDisposable ──────────────────────────────────────────────────────── */

    public void Dispose()
    {
        _scrollViewer.PointerPressed -= OnPointerPressed;
        _scrollViewer.PointerMoved -= OnPointerMoved;
        _scrollViewer.PointerReleased -= OnPointerReleased;
        _canvas.CanvasUpdated -= OnCanvasUpdated;
        _canvas.Dispose();
        GC.SuppressFinalize(this);
    }
}
