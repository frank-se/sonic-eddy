namespace SonicEddy.Controls.GraphEditorControl;

public record GraphConnection(
    IGraphNode SourceNode,
    IGraphPort SourcePort,
    IGraphNode TargetNode,
    IGraphPort TargetPort);