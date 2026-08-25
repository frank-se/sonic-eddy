using System;
using System.Linq;
using System.Threading.Tasks;
using Fr.Sonic;
using Fr.Sonic.Model.Objects;

namespace SonicEddy.Services.StreamingControl;

// Always-on, fully hardcoded - not user configurable, no picker UI, no
// persistence. Unlike CameraRouterService's slots (which route whatever
// external node the user picks), this always links the exact same source
// node whenever it exists: se.mixer-overview (SonicEddy's own mixer
// overview render - see MixerOverviewStreamService) into every compositor
// instance's third fixed input, se.video-compositor.<instance>.in2 - both
// A and B panels of the T-bar M/E switcher need the mixer-overview PIP, so
// this fans out into every name in CompositorInstanceNames.All rather than
// a single fixed target. Same watch-and-retry idiom as
// CameraRouterService/MidiRouterService - WirePlumber does not auto-link
// video streams (confirmed empirically earlier this session), so something
// has to create the link explicitly.
public sealed class MixerOverviewCompositorLinkService : IDisposable
{
    private const string SourceNodeName = "se.mixer-overview";
    private const int TargetInputIndex = 2;

    private static readonly string[] Instances = CompositorInstanceNames.All;

    private readonly object _lock = new();
    private readonly ulong?[] _linkIds = new ulong?[Instances.Length];

    public MixerOverviewCompositorLinkService()
    {
        FrSonic.PortRegistry.Added += OnPortAdded;
        FrSonic.LinkRegistry.Added += OnLinkAdded;
        FrSonic.LinkRegistry.Deleted += OnLinkDeleted;
    }

    public Task InitializeAsync()
    {
        TryConnectAll();
        return Task.CompletedTask;
    }

    private void OnPortAdded(Port port) => TryConnectAll();

    private void OnLinkAdded(Link link)
    {
        var sourcePort = FindPort(SourceNodeName, "out");
        if (sourcePort is null) return;

        for (var inst = 0; inst < Instances.Length; ++inst)
        {
            var targetPort = FindPort(TargetNodeName(inst), "in");
            if (targetPort is null || !LinkConnects(link, sourcePort, targetPort)) continue;

            lock (_lock)
                _linkIds[inst] = link.ObjectId;
        }
    }

    private void OnLinkDeleted(Link link)
    {
        var anyChanged = false;
        lock (_lock)
        {
            for (var inst = 0; inst < Instances.Length; ++inst)
            {
                if (_linkIds[inst] != link.ObjectId) continue;
                _linkIds[inst] = null;
                anyChanged = true;
            }
        }

        if (!anyChanged) return;
        // Source/target nodes may still exist (only the link died) - retry.
        TryConnectAll();
    }

    private void TryConnectAll()
    {
        for (var inst = 0; inst < Instances.Length; ++inst)
            TryConnect(inst);
    }

    private void TryConnect(int instanceIndex)
    {
        lock (_lock)
        {
            if (_linkIds[instanceIndex] is not null) return; // already connected
        }

        var sourcePort = FindPort(SourceNodeName, "out");
        var targetPort = FindPort(TargetNodeName(instanceIndex), "in");
        if (sourcePort is null || targetPort is null) return;

        var existingLink = FrSonic.LinkRegistry.Objects.FirstOrDefault(link =>
            LinkConnects(link, sourcePort, targetPort));
        if (existingLink is not null)
        {
            lock (_lock)
                _linkIds[instanceIndex] = existingLink.ObjectId;
            return;
        }

        FrSonic.LinkFactory.CreateLink(sourcePort, targetPort);
    }

    private static string TargetNodeName(int instanceIndex) =>
        CompositorInstanceNames.InputNode(Instances[instanceIndex], TargetInputIndex);

    private static bool LinkConnects(Link link, Port sourcePort, Port targetPort) =>
        link.OutputPortId == sourcePort.ObjectId && link.InputPortId == targetPort.ObjectId;

    private static Port? FindPort(string nodeName, string direction) =>
        FrSonic.PortRegistry.Objects.FirstOrDefault(port =>
            port.Direction == direction &&
            string.Equals(NodeName(port), nodeName, StringComparison.Ordinal));

    private static string? NodeName(Port port) =>
        FrSonic.NodeRegistry.GetByObjectId(port.Node.Id)?.Name;

    public void Dispose()
    {
        FrSonic.PortRegistry.Added -= OnPortAdded;
        FrSonic.LinkRegistry.Added -= OnLinkAdded;
        FrSonic.LinkRegistry.Deleted -= OnLinkDeleted;
    }
}
