namespace SonicEddy.Controls.GraphEditorControl;

public record GraphConnection(
    GraphNode SourceNode,
    GraphPort SourcePort,
    GraphNode TargetNode,
    GraphPort TargetPort);