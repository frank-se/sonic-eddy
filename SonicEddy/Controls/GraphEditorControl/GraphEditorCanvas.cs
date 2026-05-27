using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;

namespace SonicEddy.Controls.GraphEditorControl;

public class GraphEditorCanvas : Canvas, IDisposable
{
    private const double CanvasPadding = 200.0;
    private const double InitialWidth = 1200.0;
    private const double InitialHeight = 800.0;

    public GraphEditorCanvas()
    {
        Width = InitialWidth;
        Height = InitialHeight;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        GraphEdges = [];
    }

    /* ── attached property ────────────────────────────────────────────────── */

    public static readonly AttachedProperty<GraphPort?> PortProperty =
        AvaloniaProperty.RegisterAttached<GraphEditorCanvas, Control, GraphPort?>(
            "Port", defaultValue: null);

    public static GraphPort? GetPort(Control element) => element.GetValue(PortProperty);
    public static void SetPort(Control element, GraphPort? value) => element.SetValue(PortProperty, value);

    /* ── styled properties ────────────────────────────────────────────────── */

    public static readonly StyledProperty<ObservableCollection<GraphNode>?> GraphNodesProperty =
        AvaloniaProperty.Register<GraphEditorCanvas, ObservableCollection<GraphNode>?>(nameof(GraphNodes));

    public ObservableCollection<GraphNode>? GraphNodes
    {
        get => GetValue(GraphNodesProperty);
        set => SetValue(GraphNodesProperty, value);
    }

    public static readonly StyledProperty<ObservableCollection<GraphEdge>?> GraphEdgesProperty =
        AvaloniaProperty.Register<GraphEditorCanvas, ObservableCollection<GraphEdge>?>(nameof(GraphEdges));

    public ObservableCollection<GraphEdge>? GraphEdges
    {
        get => GetValue(GraphEdgesProperty);
        set => SetValue(GraphEdgesProperty, value);
    }

    public static readonly StyledProperty<GraphNode?> GraphInputsProperty =
        AvaloniaProperty.Register<GraphEditorCanvas, GraphNode?>(nameof(GraphInputs));

    public GraphNode? GraphInputs
    {
        get => GetValue(GraphInputsProperty);
        set => SetValue(GraphInputsProperty, value);
    }

    public static readonly StyledProperty<GraphNode?> GraphOutputsProperty =
        AvaloniaProperty.Register<GraphEditorCanvas, GraphNode?>(nameof(GraphOutputs));

    public GraphNode? GraphOutputs
    {
        get => GetValue(GraphOutputsProperty);
        set => SetValue(GraphOutputsProperty, value);
    }

    public static readonly StyledProperty<ICommand?> CreateEdgeCommandProperty =
        AvaloniaProperty.Register<GraphEditorCanvas, ICommand?>(nameof(CreateEdgeCommand));

    public ICommand? CreateEdgeCommand
    {
        get => GetValue(CreateEdgeCommandProperty);
        set => SetValue(CreateEdgeCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand?> DeleteEdgeCommandProperty =
        AvaloniaProperty.Register<GraphEditorCanvas, ICommand?>(nameof(DeleteEdgeCommand));

    public ICommand? DeleteEdgeCommand
    {
        get => GetValue(DeleteEdgeCommandProperty);
        set => SetValue(DeleteEdgeCommandProperty, value);
    }

    /* ── private state ────────────────────────────────────────────────────── */

    private IDisposable? _nodesSubscription;
    private IDisposable? _edgesSubscription;
    private InputsControl? _inputsControl;
    private OutputsControl? _outputsControl;

    private double _nextNodeX = 200.0;
    private double _nextNodeY = 80.0;

    /* ── property change ──────────────────────────────────────────────────── */

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == GraphNodesProperty)
        {
            _nodesSubscription?.Dispose();
            _nodesSubscription = null;
            if (change.NewValue is not ObservableCollection<GraphNode> nodes) return;
            _nodesSubscription = nodes.WeakSubscribe(OnNodesChanged);
            foreach (var node in nodes) AddControlForNode(node);
        }
        else if (change.Property == GraphEdgesProperty)
        {
            _edgesSubscription?.Dispose();
            _edgesSubscription = null;
            if (change.NewValue is not ObservableCollection<GraphEdge> edges) return;
            _edgesSubscription = edges.WeakSubscribe(OnEdgesChanged);
            foreach (var edge in edges) AddControlForEdge(edge);
        }
        else if (change.Property == GraphInputsProperty)
        {
            if (change.NewValue is not GraphNode node) return;
            if (_inputsControl is not null) Children.Remove(_inputsControl);
            _inputsControl = new InputsControl(node, this);
            Children.Add(_inputsControl);
            SetLeft(_inputsControl, 40);
            SetTop(_inputsControl, 80);
        }
        else if (change.Property == GraphOutputsProperty)
        {
            if (change.NewValue is not GraphNode node) return;
            if (_outputsControl is not null) Children.Remove(_outputsControl);
            _outputsControl = new OutputsControl(node, this);
            Children.Add(_outputsControl);
            SetLeft(_outputsControl, 700);
            SetTop(_outputsControl, 80);
        }
    }

    /* ── collection handlers ──────────────────────────────────────────────── */

    private void OnNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (var item in e.OldItems)
            {
                if (item is not GraphNode node) continue;
                var control = GetControl(node);
                if (control is not null) Children.Remove(control);
            }

        if (e.NewItems is not null)
            foreach (var item in e.NewItems)
            {
                if (item is not GraphNode node) continue;
                AddControlForNode(node);
            }
    }

    private void OnEdgesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (var item in e.OldItems)
            {
                if (item is not GraphEdge edge) continue;
                var control = GetControl(edge);
                if (control is not null) Children.Remove(control);
            }

        if (e.NewItems is not null)
            foreach (var item in e.NewItems)
            {
                if (item is not GraphEdge edge) continue;
                AddControlForEdge(edge);
            }
    }

    /* ── node / edge creation ─────────────────────────────────────────────── */

    private void AddControlForNode(GraphNode node)
    {
        var control = new NodeControl(node, this);
        SetControl(node, control);
        Children.Add(control);
    }

    private void AddControlForEdge(GraphEdge edge)
    {
        var control = new EdgeControl(edge, this);
        Children.Add(control);
    }

    /* ── canvas sizing ────────────────────────────────────────────────────── */

    public sealed class CanvasUpdatedEventArgs(double shiftX, double shiftY) : EventArgs
    {
        public double ShiftX { get; } = shiftX;
        public double ShiftY { get; } = shiftY;
    }

    public event EventHandler<CanvasUpdatedEventArgs>? CanvasUpdated;

    public void UpdateCanvasSize()
    {
        const double minPadding = 40.0;

        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = InitialWidth - CanvasPadding;
        double maxY = InitialHeight - CanvasPadding;

        foreach (var child in Children)
        {
            if (child is Line || child is not Control c) continue;
            var x = GetLeft(c);
            var y = GetTop(c);
            if (double.IsNaN(x) || double.IsNaN(y)) continue;
            var w = c.Bounds.Width > 0 ? c.Bounds.Width : 200;
            var h = c.Bounds.Height > 0 ? c.Bounds.Height : 100;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x + w);
            maxY = Math.Max(maxY, y + h);
        }

        var shiftX = minX < minPadding && minX != double.MaxValue ? minPadding - minX : 0.0;
        var shiftY = minY < minPadding && minY != double.MaxValue ? minPadding - minY : 0.0;

        if (shiftX > 0 || shiftY > 0)
        {
            foreach (var child in Children)
            {
                if (child is Line line)
                {
                    line.StartPoint = new Point(line.StartPoint.X + shiftX, line.StartPoint.Y + shiftY);
                    line.EndPoint = new Point(line.EndPoint.X + shiftX, line.EndPoint.Y + shiftY);
                    continue;
                }
                if (child is not Control c) continue;
                var x = GetLeft(c);
                var y = GetTop(c);
                if (!double.IsNaN(x)) SetLeft(c, x + shiftX);
                if (!double.IsNaN(y)) SetTop(c, y + shiftY);
            }
            maxX += shiftX;
            maxY += shiftY;
        }

        Width = maxX + CanvasPadding;
        Height = maxY + CanvasPadding;
        CanvasUpdated?.Invoke(this, new CanvasUpdatedEventArgs(shiftX, shiftY));
    }

    /* ── node positioning ─────────────────────────────────────────────────── */

    internal Point NextNodePosition()
    {
        var pos = new Point(_nextNodeX, _nextNodeY);
        _nextNodeX += 160.0;
        if (_nextNodeX > 900.0)
        {
            _nextNodeX = 200.0;
            _nextNodeY += 140.0;
        }
        return pos;
    }

    /* ── connection tracking ──────────────────────────────────────────────── */

    public void UpdateConnectionsForNodeMove(GraphNode node)
    {
        foreach (var inPort in node.InPorts)
        {
            var edges = GraphEdges?.Where(e => e.Target == inPort).ToList();
            if (edges is null || edges.Count == 0) continue;
            if (GetControl(inPort) is not PortControl portControl) continue;
            var end = portControl.GetConnectionCenterPoint(this);
            foreach (var edge in edges)
            {
                if (GetControl(edge) is not Line line) continue;
                line.EndPoint = end;
            }
        }

        foreach (var outPort in node.OutPorts)
        {
            var edges = GraphEdges?.Where(e => e.Source == outPort).ToList();
            if (edges is null || edges.Count == 0) continue;
            if (GetControl(outPort) is not PortControl portControl) continue;
            var start = portControl.GetConnectionCenterPoint(this);
            foreach (var edge in edges)
            {
                if (GetControl(edge) is not Line line) continue;
                line.StartPoint = start;
            }
        }
    }

    /* ── connection drag ──────────────────────────────────────────────────── */

    private GraphPort? _sourcePort;
    private GraphMouseState _mouseState = GraphMouseState.None;
    private Line? _newConnectionLine;

    public void StartConnectionOperation(GraphPort port, PortControl control,
        PointerPressedEventArgs eventArgs)
    {
        eventArgs.Pointer.Capture(this);
        Cursor = new Cursor(StandardCursorType.Cross);
        _sourcePort = port;
        _mouseState = GraphMouseState.CreateConnection;

        var start = control.GetConnectionCenterPoint(this);
        var end = eventArgs.GetPosition(this);
        _newConnectionLine = new Line
        {
            Stroke = Brushes.Black,
            StrokeThickness = 2,
            StartPoint = start,
            EndPoint = end,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        Children.Add(_newConnectionLine);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_mouseState != GraphMouseState.CreateConnection) return;
        var end = e.GetPosition(this);
        HighlightSelectedPort(end);
        if (_newConnectionLine is not null)
            _newConnectionLine.EndPoint = end;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);

        if (_newConnectionLine is not null)
        {
            Children.Remove(_newConnectionLine);
            _newConnectionLine = null;
        }

        if (_mouseState == GraphMouseState.CreateConnection)
        {
            var position = e.GetPosition(this);
            var selectedPort = FindSelectedPort(position);
            if (selectedPort is not null && _sourcePort is not null)
            {
                if (CreateEdgeCommand is null)
                    GraphEdges?.Add(new GraphEdge("edge", _sourcePort, selectedPort));
                else
                    CreateEdgeCommand.Execute((_sourcePort, selectedPort));
            }
            _mouseState = GraphMouseState.None;
            _sourcePort = null;
        }

        RemoveHighlights();
    }

    private List<GraphPort> AllInPorts()
    {
        List<GraphPort> ports =
        [
            ..(GraphNodes?.SelectMany(n => n.InPorts) ?? []),
            ..(GraphOutputs?.InPorts ?? [])
        ];
        return ports;
    }

    private GraphPort? FindSelectedPort(Point position)
    {
        foreach (var port in AllInPorts())
        {
            if (GetControl(port) is not PortControl p) continue;
            var center = p.GetConnectionCenterPoint(this);
            if (Point.Distance(center, position) < 10) return port;
        }
        return null;
    }

    private void HighlightSelectedPort(Point position)
    {
        RemoveHighlights();
        var port = FindSelectedPort(position);
        if (port is null) return;
        if (GetControl(port) is PortControl p) p.SetHighlight();
    }

    private void RemoveHighlights()
    {
        foreach (var port in AllInPorts())
        {
            if (GetControl(port) is PortControl p) p.RemoveHighlight();
        }
    }

    /* ── element ↔ control map ────────────────────────────────────────────── */

    private readonly Dictionary<GraphElementBase, Control> _elementToControlMap = [];

    public void SetControl(GraphElementBase element, Control control) =>
        _elementToControlMap[element] = control;

    public Control? GetControl(GraphElementBase element) =>
        _elementToControlMap.GetValueOrDefault(element);

    /* ── IDisposable ──────────────────────────────────────────────────────── */

    public void Dispose()
    {
        _nodesSubscription?.Dispose();
        _edgesSubscription?.Dispose();
        PointerMoved -= OnPointerMoved;
        PointerReleased -= OnPointerReleased;
        GC.SuppressFinalize(this);
    }
}
