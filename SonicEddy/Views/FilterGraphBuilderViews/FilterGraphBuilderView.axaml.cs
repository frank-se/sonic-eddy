using System;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using DynamicData;
using ReactiveUI.Avalonia;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels;

namespace SonicEddy.Views.FilterGraphBuilderViews;

public partial class
    FilterGraphBuilderView : ReactiveUserControl<FilterGraphBuilderViewModel>
{
    public FilterGraphBuilderView()
    {
        InitializeComponent();
    }

    private Canvas? _canvas;

    private void OnOutputNodeLoaded(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is StackPanel
            {
                DataContext: FilterChainOutputsNode outputsNode
            } panel && _canvas is not null)
        {
            outputsNode.X = _canvas.Bounds.Width - panel.Bounds.Width;
        }
    }

    private void OnCanvasLoaded(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Canvas canvas)
        {
            _canvas = canvas;
        }
    }

    private void OnPortLoaded(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Rectangle { DataContext: PortNodeBase port } rectangle)
        {
            port.RectangleRef = new(rectangle);
        }
    }

    private FilterGraphEditorDragDropState _dragDropState =
        FilterGraphEditorDragDropState.None;

    private Point? _dragDropMoveLastPosition;
    private NodeBase? _dragDropMoveNode;

    private void OnNodeHeaderPointerPressed(object? sender,
        PointerPressedEventArgs eventArgs)
    {
        if (sender is not TextBlock { DataContext: NodeBase node }) return;

        _dragDropState = FilterGraphEditorDragDropState.MoveNode;
        _dragDropMoveLastPosition = eventArgs.GetPosition(_canvas);
        _dragDropMoveNode = node;
    }

    private Line? _createConnectionPreviewLine;
    private PortNodeBase? _createConnectionSourcePort;
    private PortNodeBase? _createConnectionTargetPort;

    private void OnOutPortPointerPressed(object? sender,
        PointerPressedEventArgs eventArgs)
    {
        if (sender is not Rectangle
            {
                DataContext: PortNodeBase port
            } rectangle) return;

        _dragDropState = FilterGraphEditorDragDropState.CreateConnection;
        eventArgs.Pointer.Capture(rectangle);

        _createConnectionTargetPort = null;
        _createConnectionSourcePort = port;
        _createConnectionPreviewLine = new Line()
        {
            Stroke = Brushes.Black,
            StrokeThickness = 2,
            StrokeDashArray = [4, 2],
            StartPoint = port.GetRectCenter(_canvas!),
            EndPoint = eventArgs.GetPosition(_canvas)
        };

        _canvas?.Children.Add(_createConnectionPreviewLine);
        Cursor = new Cursor(StandardCursorType.Cross);
    }

    private void OnConnectionDragPointerMoved(object? sender,
        PointerEventArgs eventArgs)
    {
        if (_dragDropState == FilterGraphEditorDragDropState.CreateConnection)
        {
            if (_createConnectionSourcePort == null ||
                _createConnectionPreviewLine == null) return;

            var currentPosition = eventArgs.GetPosition(_canvas);
            _createConnectionPreviewLine.EndPoint = currentPosition;

            var targetPort = FindClosestInPort(currentPosition);
            if (_createConnectionTargetPort is not null &&
                _createConnectionTargetPort != targetPort)
            {
                _createConnectionTargetPort.Rectangle?.Fill = Brushes.Green;
            }

            targetPort?.Rectangle?.Fill = Brushes.Chartreuse;
            _createConnectionTargetPort = targetPort;
        }
        else if (_dragDropState == FilterGraphEditorDragDropState.MoveNode)
        {
            if (_dragDropMoveLastPosition is null ||
                _dragDropMoveNode is null) return;

            var currentPosition = eventArgs.GetPosition(_canvas);

            var diffX = _dragDropMoveLastPosition?.X - currentPosition.X;
            var diffY = _dragDropMoveLastPosition?.Y - currentPosition.Y;
            _dragDropMoveNode.X -= diffX ?? 0;
            _dragDropMoveNode.Y -= diffY ?? 0;
            _dragDropMoveLastPosition = currentPosition;
        }
    }

    private void OnConnectionDragPointerRelease(object? sender,
        PointerReleasedEventArgs eventArgs)
    {
        if (_dragDropState == FilterGraphEditorDragDropState.None) return;
        eventArgs.Pointer.Capture(null);
        Cursor = new Cursor(StandardCursorType.Arrow);

        if (_dragDropState == FilterGraphEditorDragDropState.CreateConnection)
        {
            if (_createConnectionPreviewLine != null)
            {
                _canvas?.Children.Remove(_createConnectionPreviewLine);
                _createConnectionPreviewLine = null;
            }

            if (_createConnectionTargetPort is not null &&
                _createConnectionSourcePort is not null)
            {
                if (_canvas?.DataContext is FilterGraphBuilderViewModel
                    viewModel)
                {
                    viewModel.Connect(_createConnectionSourcePort,
                        _createConnectionTargetPort);
                }

                _createConnectionTargetPort?.Rectangle?.Fill = Brushes.Green;
                _createConnectionTargetPort = null;
                _createConnectionSourcePort = null;
            }
        }
        else if (_dragDropState == FilterGraphEditorDragDropState.MoveNode)
        {
            if (_canvas?.DataContext is FilterGraphBuilderViewModel
                    viewModel && _dragDropMoveNode is not null)
            {
                var connectionsToUpdate =
                    viewModel.Connections.Where(connection =>
                        _dragDropMoveNode.InPorts.Contains(connection.InPort) ||
                        _dragDropMoveNode.OutPorts.Contains(connection
                            .OutPort)).ToList();

                foreach (var connection in connectionsToUpdate)
                {
                    viewModel.Connections.Remove(connection);
                }
                
                viewModel.Connections.AddRange(connectionsToUpdate);
            }

            _dragDropMoveLastPosition = null;
            _dragDropMoveNode = null;
        }

        _dragDropState = FilterGraphEditorDragDropState.None;
    }

    private PortNodeBase? FindClosestInPort(Point position)
    {
        if (DataContext is not FilterGraphBuilderViewModel viewModel ||
            _canvas == null)
            return null;

        var inPorts = viewModel.Nodes.SelectMany(n => n.InPorts)
            .Where(p => p.IsPointInHotZone(_canvas, position)).ToList();

        return !inPorts.Any() ? null : inPorts.First();
    }
}