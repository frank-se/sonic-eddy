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

public class GraphEditor : Canvas, IDisposable
{
    static GraphEditor()
    {
        AffectsArrange<GraphEditor>(GraphNodesProperty);
        AffectsRender<GraphEditor>(GraphNodesProperty);
    }

    public GraphEditor()
    {
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        GraphEdges = [];
    }

    public static readonly AttachedProperty<GraphPort?> PortProperty =
        AvaloniaProperty.RegisterAttached<GraphEditor, Control, GraphPort?>(
            name: "Port",
            defaultValue: null);

    public static GraphPort? GetPort(Control element) =>
        element.GetValue(PortProperty);

    public static void SetPort(Control element, GraphPort? value) =>
        element.SetValue(PortProperty, value);

    public static readonly StyledProperty<ObservableCollection<GraphNode>?>
        GraphNodesProperty =
            AvaloniaProperty
                .Register<GraphEditor, ObservableCollection<GraphNode>?>(
                    nameof(GraphNodes));

    public ObservableCollection<GraphNode>? GraphNodes
    {
        get => GetValue(GraphNodesProperty);
        set => SetValue(GraphNodesProperty, value);
    }

    public static readonly StyledProperty<ICommand?> CreateEdgeCommandProperty =
        AvaloniaProperty.Register<GraphEditor, ICommand?>(
            nameof(CreateEdgeCommand));

    public ICommand? CreateEdgeCommand
    {
        get => GetValue(CreateEdgeCommandProperty);
        set => SetValue(CreateEdgeCommandProperty, value);
    }

    private IDisposable? _nodesCollectionChangeSubscription;

    public static readonly StyledProperty<ICommand?> DeleteEdgeCommandProperty =
        AvaloniaProperty.Register<GraphEditor, ICommand?>(
            nameof(DeleteEdgeCommand));

    public ICommand? DeleteEdgeCommand
    {
        get => GetValue(DeleteEdgeCommandProperty);
        set => SetValue(DeleteEdgeCommandProperty, value);
    }

    public static readonly StyledProperty<ObservableCollection<GraphEdge>?>
        GraphEdgesProperty =
            AvaloniaProperty
                .Register<GraphEditor, ObservableCollection<GraphEdge>?>(
                    nameof(GraphEdges));

    public ObservableCollection<GraphEdge>? GraphEdges
    {
        get => GetValue(GraphEdgesProperty);
        set => SetValue(GraphEdgesProperty, value);
    }

    private IDisposable? _edgesCollectionChangeSubscription;

    public static readonly StyledProperty<GraphNode?>
        GraphInputsProperty =
            AvaloniaProperty.Register<GraphEditor, GraphNode?>(
                nameof(GraphInputs));

    public GraphNode? GraphInputs
    {
        get => GetValue(GraphInputsProperty);
        set => SetValue(GraphInputsProperty, value);
    }

    private InputsControl? _inputsControl;

    public static readonly StyledProperty<GraphNode?>
        GraphOutputsProperty =
            AvaloniaProperty.Register<GraphEditor, GraphNode?>(
                nameof(GraphOutputs));

    public GraphNode? GraphOutputs
    {
        get => GetValue(GraphOutputsProperty);
        set => SetValue(GraphOutputsProperty, value);
    }

    private OutputsControl? _outputsControl;

    protected override void OnPropertyChanged(
        AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == GraphNodesProperty)
        {
            _nodesCollectionChangeSubscription?.Dispose();
            _nodesCollectionChangeSubscription = null;
            if (change.NewValue is not ObservableCollection<GraphNode> nodes)
                return;

            _nodesCollectionChangeSubscription =
                nodes.WeakSubscribe(OnNodesCollectionChangedEvent);

            foreach (var node in nodes)
            {
                AddControlForNode(node);
            }
        }
        else if (change.Property == GraphEdgesProperty)
        {
            _edgesCollectionChangeSubscription?.Dispose();
            _edgesCollectionChangeSubscription = null;
            if (change.NewValue is not ObservableCollection<GraphEdge> edges)
                return;

            _edgesCollectionChangeSubscription =
                edges.WeakSubscribe(OnEdgesCollectionChangedEvent);

            foreach (var edge in edges)
            {
                AddControlForEdge(edge);
            }
        }
        else if (change.Property == GraphInputsProperty)
        {
            if (change.NewValue is not GraphNode node)
                return;

            if (_inputsControl is not null)
                Children.Remove(_inputsControl);

            _inputsControl = new(node, this);
            Children.Add(_inputsControl);
        }
        else if (change.Property == GraphOutputsProperty)
        {
            if (change.NewValue is not GraphNode node)
                return;

            if (_outputsControl is not null)
                Children.Remove(_outputsControl);

            _outputsControl = new(node, this);
            Children.Add(_outputsControl);
            var left = Bounds.Width - _outputsControl.Bounds.Width;
            SetLeft(_outputsControl, left);
            SetTop(_outputsControl, 0);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_outputsControl is null) return;

        var left = Bounds.Width - _outputsControl.Bounds.Width;
        SetLeft(_outputsControl, left);
    }

    private void OnEdgesCollectionChangedEvent(object? sender,
        NotifyCollectionChangedEventArgs eventArgs)
    {
        if (eventArgs.OldItems is not null)
            foreach (var old in eventArgs.OldItems)
            {
                if (old is not GraphEdge edge) continue;

                var control = GetControl(edge);
                if (control is not null)
                    Children.Remove(control);
            }

        if (eventArgs.NewItems is not null)
            foreach (var newItem in eventArgs.NewItems)
            {
                if (newItem is not GraphEdge edge) continue;
                AddControlForEdge(edge);
            }
    }

    private void OnNodesCollectionChangedEvent(object? sender,
        NotifyCollectionChangedEventArgs eventArgs)
    {
        if (eventArgs.OldItems is not null)
            foreach (var old in eventArgs.OldItems)
            {
                if (old is not GraphNode node) continue;

                var control = GetControl(node);
                if (control is not null)
                    Children.Remove(control);
            }

        if (eventArgs.NewItems is not null)
            foreach (var newItem in eventArgs.NewItems)
            {
                if (newItem is not GraphNode node) continue;
                AddControlForNode(node);
            }
    }

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

    public void UpdateConnectionsForNodeMove(GraphNode node)
    {
        foreach (var inPort in node.InPorts)
        {
            var edges = GraphEdges?.Where(e => e.Target == inPort).ToList();
            if (edges is null || edges is { Count: <= 0 }) continue;

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
            if (edges is null || edges is { Count: <= 0 }) continue;

            if (GetControl(outPort) is not PortControl portControl) continue;
            var start = portControl.GetConnectionCenterPoint(this);
            foreach (var edge in edges)
            {
                if (GetControl(edge) is not Line line) continue;
                line.StartPoint = start;
            }
        }
    }

    private GraphPort? _sourcePort;
    private Control? _sourceControl;
    private GraphMouseState _mouseState = GraphMouseState.None;
    private Line? _newConnectionLine;

    public void StartConnectionOperation(GraphPort port, PortControl control,
        PointerPressedEventArgs eventArgs)
    {
        eventArgs.Pointer.Capture(this);
        Cursor = new(StandardCursorType.Cross);
        _sourcePort = port;
        _sourceControl = control;
        _mouseState = GraphMouseState.CreateConnection;
        var start = control.GetConnectionCenterPoint(this);
        var end = eventArgs.GetPosition(this);
        _newConnectionLine = new Line()
        {
            Stroke = Brushes.Black,
            StrokeThickness = 2,
            StartPoint = start,
            EndPoint = end,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        Children.Add(_newConnectionLine);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs eventArgs)
    {
        switch (_mouseState)
        {
            case GraphMouseState.CreateConnection:
                var end = eventArgs.GetPosition(this);
                HighlightSelectedPort(end);
                _newConnectionLine?.EndPoint = end;
                break;
            case GraphMouseState.None:
            default:
                break;
        }
    }

    private void OnPointerReleased(object? sender,
        PointerReleasedEventArgs eventArgs)
    {
        eventArgs.Pointer.Capture(null);

        if (_newConnectionLine is not null)
        {
            Children.Remove(_newConnectionLine);
            _newConnectionLine = null;
        }

        if (_mouseState == GraphMouseState.CreateConnection)
        {
            var position = eventArgs.GetPosition(this);
            var selectedPort = FindSelectedPort(position);
            if (selectedPort is not null && _sourcePort is not null &&
                CreateEdgeCommand is null)
                GraphEdges?.Add(new("edge", _sourcePort, selectedPort));
            else if (selectedPort is not null && _sourcePort is not null &&
                     CreateEdgeCommand is not null)
                CreateEdgeCommand.Execute((_sourcePort!, selectedPort!));
            _mouseState = GraphMouseState.None;
            _sourcePort = null;
        }

        RemoveHighlights();
    }

    private List<GraphPort> AllInPorts()
    {
        if (GraphNodes is null && GraphOutputs is null) return [];

        List<GraphPort> ports =
        [
            ..(GraphNodes?.SelectMany(n => n.InPorts) ?? []),
            ..(GraphOutputs?.InPorts ?? [])
        ];

        return ports;
    }

    private GraphPort? FindSelectedPort(Point position)
    {
        var ports = AllInPorts();
        foreach (var port in ports)
        {
            if (GetControl(port) is not PortControl p) continue;

            var center = p.GetConnectionCenterPoint(this);
            var distance = Point.Distance(center, position);
            if (distance < 10) return port;
        }

        return null;
    }

    private void HighlightSelectedPort(Point position)
    {
        RemoveHighlights();
        var port = FindSelectedPort(position);
        if (port is null) return;

        if (GetControl(port) is not PortControl p) return;
        p.SetHighlight();
    }

    private void RemoveHighlights()
    {
        var ports = AllInPorts();

        foreach (var port in ports)
        {
            if (GetControl(port) is not PortControl p) continue;
            p.RemoveHighlight();
        }
    }

    private readonly Dictionary<GraphElementBase, Control>
        _elementToControlMap = [];

    public void SetControl(GraphElementBase element, Control control)
    {
        _elementToControlMap[element] = control;
    }

    public Control? GetControl(GraphElementBase element) =>
        _elementToControlMap.GetValueOrDefault(element);

    public void Dispose()
    {
        _nodesCollectionChangeSubscription?.Dispose();
        _edgesCollectionChangeSubscription?.Dispose();
        PointerMoved -= OnPointerMoved;
        PointerReleased -= OnPointerReleased;
        GC.SuppressFinalize(this);
    }
}