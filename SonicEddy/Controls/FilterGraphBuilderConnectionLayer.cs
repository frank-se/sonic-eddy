using System;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels;

namespace SonicEddy.Controls;

public class FilterGraphBuilderConnectionLayer : Canvas
{
    static FilterGraphBuilderConnectionLayer()
    {
        AffectsRender<FilterGraphBuilderConnectionLayer>(ConnectionsProperty);
    }

    public static readonly StyledProperty<ObservableCollection<PortConnection>?>
        ConnectionsProperty =
            AvaloniaProperty
                .Register<FilterGraphBuilderConnectionLayer,
                    ObservableCollection<PortConnection>?>(nameof(Connections));
    
    public ObservableCollection<PortConnection>? Connections
    {
        get => GetValue(ConnectionsProperty);
        set => SetValue(ConnectionsProperty, value);
    }

    private IDisposable? _collectionSubscription;

    protected override void OnPropertyChanged(
        AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ConnectionsProperty)
        {
            _collectionSubscription?.Dispose();
            _collectionSubscription = null;
            if (change.NewValue is not ObservableCollection<PortConnection>
                
                newConnections) return;
            _collectionSubscription =
                newConnections.WeakSubscribe((s, e) => UpdateConnections());

            UpdateConnections();
        }
        else
        {
            Children.Clear();
        }
    }

    private void UpdateConnections()
    {
        Children.Clear();

        if (Connections == null) return;

        foreach (var connection in Connections)
        {
            var start = connection.OutPort.GetRectCenter(this);
            var end = connection.InPort.GetRectCenter(this);

            var line = new Line()
            {
                Stroke = Brushes.Black,
                StrokeThickness = 2,
                StartPoint = start,
                EndPoint = end,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            Children.Add(line);
        }
    }
}