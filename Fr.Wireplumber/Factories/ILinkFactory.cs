using Fr.Wireplumber.Model.Objects;

namespace Fr.Wireplumber.Factories;

/// <summary>
/// Create and delete links between pipewire ports
/// </summary>
public interface ILinkFactory
{
    /// <summary>
    /// Connect the <c>outputPort</c> to the <c>inputPort</c>
    /// </summary>
    /// <param name="outputPort">The output port to start the link from</param>
    /// <param name="inputPort">The input port to end the link at</param>
    void CreateLink(Port outputPort, Port inputPort);

    /// <summary>
    /// Delete a link
    /// </summary>
    /// <param name="link">The link to delete</param>
    void DeleteLink(Link link);
}