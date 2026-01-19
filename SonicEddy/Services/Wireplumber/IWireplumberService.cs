using System.Collections.Generic;
using System.Threading.Tasks;
using Fr.Wireplumber.Model.Objects;

namespace SonicEddy.Services.Wireplumber;

public interface IWireplumberService
{
    List<Node> GetTargetObjectsForCaptureNode();
    List<Node> GetTargetObjectsForPlaybackNode();
}