using System;
using System.Linq;
using System.Threading.Tasks;
using Fr.Sonic;
using Fr.Sonic.Model.Objects;

namespace SonicEddy.Services.StreamingControl;

// Always-on, fully hardcoded - not user configurable, no picker UI, no
// persistence. Unlike CameraRouterService's slots (which route whatever
// external node the user picks), this always links the exact same two
// named nodes whenever both exist: se.mixer-overview (SonicEddy's own
// mixer overview render - see MixerOverviewStreamService) into the
// compositor's third fixed input, se.video-compositor.in2. Same
// watch-and-retry idiom as CameraRouterService/MidiRouterService -
// WirePlumber does not auto-link video streams (confirmed empirically
// earlier this session), so something has to create the link explicitly.
public sealed class MixerOverviewCompositorLinkService : IDisposable
{
    private const string SourceNodeName = "se.mixer-overview";
    private const string TargetNodeName = "se.video-compositor.in2";

    private readonly object _lock = new();
    private ulong? _linkId;

    public MixerOverviewCompositorLinkService()
    {
        FrSonic.PortRegistry.Added += OnPortAdded;
        FrSonic.LinkRegistry.Added += OnLinkAdded;
        FrSonic.LinkRegistry.Deleted += OnLinkDeleted;
    }

    public Task InitializeAsync()
    {
        TryConnect();
        return Task.CompletedTask;
    }

    private void OnPortAdded(Port port) => TryConnect();

    private void OnLinkAdded(Link link)
    {
        var sourcePort = FindPort(SourceNodeName, "out");
        var targetPort = FindPort(TargetNodeName, "in");
        if (sourcePort is null || targetPort is null) return;
        if (!LinkConnects(link, sourcePort, targetPort)) return;

        lock (_lock)
            _linkId = link.ObjectId;
    }

    private void OnLinkDeleted(Link link)
    {
        lock (_lock)
        {
            if (_linkId != link.ObjectId) return;
            _linkId = null;
        }

        // Source/target nodes may still exist (only the link died) - retry.
        TryConnect();
    }

    private void TryConnect()
    {
        lock (_lock)
        {
            if (_linkId is not null) return; // already connected
        }

        var sourcePort = FindPort(SourceNodeName, "out");
        var targetPort = FindPort(TargetNodeName, "in");
        if (sourcePort is null || targetPort is null) return;

        var existingLink = FrSonic.LinkRegistry.Objects.FirstOrDefault(link =>
            LinkConnects(link, sourcePort, targetPort));
        if (existingLink is not null)
        {
            lock (_lock)
                _linkId = existingLink.ObjectId;
            return;
        }

        FrSonic.LinkFactory.CreateLink(sourcePort, targetPort);
    }

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
