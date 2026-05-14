using Fr.Wireplumber.Model.Objects;

namespace Fr.Wireplumber.Factories.Implementation;

internal class LinkFactory : ILinkFactory
{
    public void CreateLink(Port outputPort, Port inputPort) =>
        PInvoke.FrWireplumberLib.CreateLinkByPortId(
            outputPort.ObjectId, inputPort.ObjectId, false);

    public void DeleteLink(Link link) =>
        PInvoke.FrWireplumberLib.DeleteLinkByObjectId(link.ObjectId);
}