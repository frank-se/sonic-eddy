using System.Collections.Generic;
using System.Linq;
using Fr.Wireplumber.Model.Objects;

namespace SonicEddy.Services.Wireplumber;

public class WireplumberService : IWireplumberService
{
    public List<Node> GetTargetObjectsForCaptureNode() => GetPlaybackNodes();

    public List<Node> GetTargetObjectsForPlaybackNode() => 
        Fr.Wireplumber.Wireplumber.NodeRegistry.Objects.Where(n =>
            n.Media.Class is "Audio/Sink" or "Stream/Input/Audio").ToList();

    public List<Node> GetPlaybackNodes() =>
        Fr.Wireplumber.Wireplumber.NodeRegistry.Objects.Where(n =>
            n.Media.Class is "Audio/Source" or "Stream/Output/Audio").ToList();
}