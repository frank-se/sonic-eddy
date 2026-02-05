using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using ReactiveUI;
using SonicEddy.Controls.GraphEditorControl;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph;

public abstract class PortViewModelBase(
    string name) : GraphPort(name)
{
    private Guid _id = Guid.NewGuid();

    public Guid Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
    }

    public WeakReference<Rectangle>? RectangleRef { get; set; }

    public Rectangle? Rectangle
    {
        get
        {
            if (RectangleRef?.TryGetTarget(out var rectangle) != true ||
                rectangle == null)
                return null;
            return rectangle;
        }
    }

    public Point GetRectCenter(Canvas parent)
    {
        if (RectangleRef?.TryGetTarget(out var rectangle) != true ||
            rectangle == null)
            return default;

        var matrix = rectangle.TransformToVisual(parent);
        if (!matrix.HasValue) return default;

        var center = new Point(rectangle.Bounds.Width / 2,
            rectangle.Bounds.Height / 2);
        return matrix.Value.Transform(center);
    }

    public bool IsPointInHotZone(Canvas parent, Point position)
    {
        if (RectangleRef?.TryGetTarget(out var rectangle) != true ||
            rectangle == null)
            return false;

        var center = GetRectCenter(parent);
        var selectionRadius = rectangle.Width + rectangle.Margin.Left / 2;
        var distance = Point.Distance(center, position);
        return distance < selectionRadius;
    }
}