using Fr.Sonic.Model.Objects;
using Fr.Sonic.PInvoke;

namespace Fr.Sonic.Factories.Implementation;

internal class LinkFactory : ILinkFactory
{
    public void CreateLink(Port outputPort, Port inputPort) =>
        FrSonicLib.CreateLinkByPortIdsC(outputPort.ObjectId, inputPort.ObjectId, false);

    public void DeleteLink(Link link) =>
        FrSonicLib.DeleteLinkByObjectIdC(link.ObjectId);
}