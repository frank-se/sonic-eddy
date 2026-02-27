using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fr.Wireplumber.Model.Config.LoopbackModule;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Modules.Models;

namespace SonicEddy.Services.Wireplumber;

public class WireplumberService : IWireplumberService, IDisposable
{
    public WireplumberService()
    {
        Fr.Wireplumber.Wireplumber.NodeRegistry.Added += OnNodeAdded;
        Fr.Wireplumber.Wireplumber.NodeRegistry.Deleted += OnNodeDeleted;
    }

    private void OnNodeAdded(Node node) => NodeAdded?.Invoke(node);
    private void OnNodeDeleted(Node node) => NodeDeleted?.Invoke(node);

    public List<Port> GetPortsForNode(Node node) =>
        Fr.Wireplumber.Wireplumber.PortRegistry.Objects.Where(p =>
            p.Node.Id == node?.ObjectId).ToList();

    public List<Node> GetTargetObjectsForCaptureNode() => GetPlaybackNodes();

    public List<Node> GetTargetObjectsForPlaybackNode() =>
        Fr.Wireplumber.Wireplumber.NodeRegistry.Objects.Where(n =>
            n.Media.Class is "Audio/Sink" or "Stream/Input/Audio").ToList();

    public List<Node> GetPlaybackNodes() =>
        Fr.Wireplumber.Wireplumber.NodeRegistry.Objects.Where(IsPlaybackNode)
            .ToList();

    public List<Node> GetCaptureNodes() =>
        Fr.Wireplumber.Wireplumber.NodeRegistry.Objects.Where(IsCaptureNode)
            .ToList();

    public bool IsPlaybackNode(Node node) =>
        node.Media.Class is "Audio/Source" or "Stream/Output/Audio";

    public bool IsCaptureNode(Node node) =>
        node.Media.Class is "Audio/Sink" or "Stream/Input/Audio";

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

    public event Action<Node>? NodeAdded;
    public event Action<Node>? NodeDeleted;

    public void Dispose()
    {
        Fr.Wireplumber.Wireplumber.NodeRegistry.Added -= OnNodeAdded;

        GC.SuppressFinalize(this);
    }
}