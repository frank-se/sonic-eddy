using Avalonia.Controls;

namespace SonicEddy.Controls.GraphEditorControl;

public interface IGraphPort
{
    string Name { get; }
    Control? Control { get; set; }
    
}