using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fr.Wireplumber.Model.Config.LoopbackModule;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Modules.Models;

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

    public List<Node> GetCaptureNodes() =>
        Fr.Wireplumber.Wireplumber.NodeRegistry.Objects.Where(n =>
            n.Media.Class is "Audio/Sink" or "Stream/Input/Audio").ToList();

    public List<Port> GetMidiPorts()
    {
        var midiBridgeNodes =
            Fr.Wireplumber.Wireplumber.NodeRegistry.Objects.Where(n =>
                n.Media.Class == "Midi/Bridge").Select(n => n.ObjectId);

        return Fr.Wireplumber.Wireplumber.PortRegistry.Objects.Where(p =>
            midiBridgeNodes.Contains(p.Node.Id)).ToList();
    }

    public Task<LoopbackModule> CreateLoopbackModule(string name,
        LoopbackModuleConfig config) =>
        Fr.Wireplumber.Wireplumber.ModuleFactory.CreateLoopbackModuleAsync(name,
            config);
}