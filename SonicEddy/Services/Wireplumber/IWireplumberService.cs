using System.Collections.Generic;
using System.Threading.Tasks;
using Fr.Wireplumber.Model.Config.LoopbackModule;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Modules.Models;

namespace SonicEddy.Services.Wireplumber;

public interface IWireplumberService
{
    List<Node> GetTargetObjectsForCaptureNode();
    List<Node> GetTargetObjectsForPlaybackNode();
    List<Node> GetPlaybackNodes();
    List<Node> GetCaptureNodes();
    List<Port> GetMidiPorts();

    Task<LoopbackModule> CreateLoopbackModule(string name,
        LoopbackModuleConfig config);
}