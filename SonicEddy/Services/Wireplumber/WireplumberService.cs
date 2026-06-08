using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fr.Sonic.Model.Config.LoopbackModule;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;
using SonicEddy.Services.DrumMixer;
using SonicEddy.Services.VirtualInputs;

namespace SonicEddy.Services.Wireplumber;

public class WireplumberService : IWireplumberService, IDisposable
{
    public WireplumberService()
    {
        Fr.Sonic.FrSonic.NodeRegistry.Added += OnNodeAdded;
        Fr.Sonic.FrSonic.NodeRegistry.Deleted += OnNodeDeleted;
    }

    private void OnNodeAdded(Node node) => NodeAdded?.Invoke(node);
    private void OnNodeDeleted(Node node) => NodeDeleted?.Invoke(node);

    public List<Port> GetPortsForNode(Node node) =>
        Fr.Sonic.FrSonic.PortRegistry.Objects.Where(p =>
            p.Node.Id == node?.ObjectId).ToList();

    public List<Node> GetTargetObjectsForCaptureNode() => GetPlaybackNodes();

    public List<Node> GetTargetObjectsForPlaybackNode() =>
        Fr.Sonic.FrSonic.NodeRegistry.Objects.Where(n =>
            n.Media.Class is "Audio/Sink" or "Stream/Input/Audio").ToList();

    public List<Node> GetPlaybackNodes() =>
        Fr.Sonic.FrSonic.NodeRegistry.Objects
            .Where(n => IsPlaybackNode(n) && !IsInternalNode(n))
            .ToList();

    public List<Node> GetCaptureNodes() =>
        Fr.Sonic.FrSonic.NodeRegistry.Objects
            .Where(n => IsCaptureNode(n) && !IsInternalNode(n))
            .ToList();

    private static bool IsInternalNode(Node node) =>
        node.Name?.StartsWith("silence-") == true ||
        node.Name?.StartsWith("monitor ") == true ||
        (!string.IsNullOrEmpty(node.Pmx.Tag) &&
         node.Name != DrumMixerService.PlaybackNodeName &&
         !VirtualInputService.IsVirtualInputPlaybackNode(node));

    public bool IsPlaybackNode(Node node) =>
        node.Media.Class is "Audio/Source" or "Stream/Output/Audio";

    public bool IsCaptureNode(Node node) =>
        node.Media.Class is "Audio/Sink" or "Stream/Input/Audio";

    public List<Port> GetMidiPorts()
    {
        var midiBridgeNodes =
            Fr.Sonic.FrSonic.NodeRegistry.Objects.Where(n =>
                n.Media.Class == "Midi/Bridge").Select(n => n.ObjectId);

        return Fr.Sonic.FrSonic.PortRegistry.Objects.Where(p =>
            midiBridgeNodes.Contains(p.Node.Id)).ToList();
    }

    public Task<LoopbackModule> CreateLoopbackModule(string name,
        LoopbackModuleConfig config) =>
        Fr.Sonic.FrSonic.ModuleFactory.CreateLoopbackModuleAsync(name,
            config);

    public event Action<Node>? NodeAdded;
    public event Action<Node>? NodeDeleted;

    public void Dispose()
    {
        Fr.Sonic.FrSonic.NodeRegistry.Added -= OnNodeAdded;

        GC.SuppressFinalize(this);
    }
}