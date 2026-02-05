namespace SonicEddy.Controls.GraphEditorControl;

public class GraphEdge(
    string name,
    GraphPort source,
    GraphPort target) : GraphElementBase(name)
{
    public GraphPort Source { get; } = source;
    public GraphPort Target { get; } = target;
}