using Avalonia;
using ReactiveUI;

namespace SonicEddy.Controls.GraphEditorControl;

public class GraphElementBase(string name) : ReactiveObject
{
    public string Name { get; } = name;
}