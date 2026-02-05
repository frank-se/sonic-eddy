using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SonicEddy.Controls.GraphEditorControl;

public class InputsControl : StackPanel
{
    private readonly GraphNode _node;

    public InputsControl(GraphNode node, GraphEditor editor)
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

        Canvas.SetLeft(this, 0);
        Canvas.SetTop(this, 0);

        foreach (var outPort in node.OutPorts)
        {
            var control = new PortControl(PortType.Out, PortTextSide.Left,
                outPort, editor);

            Children.Add(control);

            editor.SetControl(outPort, control);
            GraphEditor.SetPort(control, outPort);
        }
    }
}