using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using ReactiveUI;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels;

public abstract class PortNodeBase(string name, NodeBase node)
{
    public string Name { get; init; } = name;
    public NodeBase Node { get; init; } = node;

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