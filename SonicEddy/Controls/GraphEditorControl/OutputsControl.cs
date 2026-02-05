using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SonicEddy.Controls.GraphEditorControl;

public class OutputsControl : StackPanel
{
    private readonly GraphNode _node;

    public OutputsControl(GraphNode node, GraphEditor editor)
    {
        _node = node;
        Orientation = Orientation.Vertical;

        Background = Brushes.Bisque;

        Children.Add(new TextBlock()
        {
            Text = node.Name,
            Margin = new(6, 4, 4, 4),
            Foreground = Brushes.Black,
            FontWeight = FontWeight.Bold
        });

        foreach (var inPort in node.InPorts)
        {
            var control = new PortControl(PortType.In, PortTextSide.Right,
                inPort, editor);

            Children.Add(control);

            editor.SetControl(inPort, control);
            GraphEditor.SetPort(control, inPort);
        }
    }
}