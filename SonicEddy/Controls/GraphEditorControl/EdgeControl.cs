using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;

namespace SonicEddy.Controls.GraphEditorControl;

public class EdgeControl : Line
{
    public EdgeControl(GraphEdge edge, GraphEditorCanvas canvas)
    {
        Stroke = Brushes.Black;
        StrokeThickness = 2;
        Cursor = new Cursor(StandardCursorType.Hand);
        StrokeLineCap = PenLineCap.Round;

        var sourceControl = canvas.GetControl(edge.Source);
        var targetControl = canvas.GetControl(edge.Target);

        if (sourceControl is not PortControl s || targetControl is not PortControl t) return;

        StartPoint = s.GetConnectionCenterPoint(canvas);
        EndPoint = t.GetConnectionCenterPoint(canvas);

        canvas.SetControl(edge, this);
    }
}
