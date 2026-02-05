using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;

namespace SonicEddy.Controls.GraphEditorControl;

public class EdgeControl : Line
{
    public EdgeControl(GraphEdge edge, GraphEditor editor)
    {
        Stroke = Brushes.Black;
        StrokeThickness = 2;
        Cursor = new(StandardCursorType.Hand);
        StrokeLineCap = PenLineCap.Round;

        var sourceControl = editor.GetControl(edge.Source);
        var targetControl = editor.GetControl(edge.Target);

        if (sourceControl is not PortControl s ||
            targetControl is not PortControl t) return;

        var start = s.GetConnectionCenterPoint(editor);
        var end = t.GetConnectionCenterPoint(editor);

        StartPoint = start;
        EndPoint = end;
        
        editor.SetControl(edge, this);
    }
}