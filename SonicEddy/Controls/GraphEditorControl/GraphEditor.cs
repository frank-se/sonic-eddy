using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;

namespace SonicEddy.Controls.GraphEditorControl;

public class GraphEditor : Canvas, IDisposable
{
    static GraphEditor()
    {
        AffectsArrange<GraphEditor>(GraphNodesProperty);
        AffectsRender<GraphEditor>(GraphNodesProperty);
    }

    public static readonly StyledProperty<ObservableCollection<IGraphNode>?>
        GraphNodesProperty =
            AvaloniaProperty
                .Register<GraphEditor, ObservableCollection<IGraphNode>?>(
                    nameof(GraphNodes));

    public ObservableCollection<IGraphNode>? GraphNodes
    {
        get => GetValue(GraphNodesProperty);
        set => SetValue(GraphNodesProperty, value);
    }

    private IDisposable? _nodesCollectionChangeSubscription;

    protected override void OnPropertyChanged(
        AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == GraphNodesProperty)
        {
            _nodesCollectionChangeSubscription?.Dispose();
            _nodesCollectionChangeSubscription = null;
            if (change.NewValue is not ObservableCollection<IGraphNode> nodes)
                return;

            _nodesCollectionChangeSubscription =
                nodes.WeakSubscribe(OnNodesCollectionChangedEvent);

            foreach (var node in nodes)
            {
                AddControlForNode(node);
            }
        }
    }

    private void OnNodesCollectionChangedEvent(object? sender,
        NotifyCollectionChangedEventArgs eventArgs)
    {
        if (eventArgs.OldItems is not null)
            foreach (var old in eventArgs.OldItems)
            {
                if (old is IGraphNode { Control: not null } node)
                    Children.Remove(node.Control);
            }
        
        if (eventArgs.NewItems is not null)
            foreach (var newItem in eventArgs.NewItems)
            {
                if (newItem is not IGraphNode node) continue;
                AddControlForNode(node);
            }
    }

    private void AddControlForNode(IGraphNode node)
    {
        var control = new NodeControl(node);
        node.Control = control;
        Children.Add(control);
    }

    public void Dispose()
    {
        _nodesCollectionChangeSubscription?.Dispose();
        GC.SuppressFinalize(this);
    }
}