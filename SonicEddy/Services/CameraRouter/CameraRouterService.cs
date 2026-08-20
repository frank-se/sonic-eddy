using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fr.Sonic;
using Fr.Sonic.Model.Objects;
using SonicEddy.Contracts.CameraRouter;
using SonicEddy.Services.AppData;

namespace SonicEddy.Services.CameraRouter;

// WirePlumber does not auto-link video streams the way it does audio/MIDI
// (confirmed empirically - target.object has no effect on video nodes), so
// this service does it: watches for named camera-like source nodes and
// links them into pw-video-compositor's fixed se.video-compositor.in{N}
// input nodes, either immediately (if already present) or as soon as they
// appear. Mirrors Services/MidiRouter/MidiRouterService.cs closely - same
// "match by node name, retry on every port-added event" shape.
public sealed class CameraRouterService : ICameraRouterService, IDisposable
{
    public const int SlotCount = 2;

    // Sonic Eddy's own video nodes never show up as pickable camera sources.
    private static readonly HashSet<string> OwnNodeNames =
    [
        "se.video-compositor.in0",
        "se.video-compositor.in1",
        "se.video-compositor.out",
        "se.mixer-overview",
    ];

    private readonly IAppDataService _appDataService;
    private readonly string?[] _assignments = new string?[SlotCount];
    private readonly ulong?[] _linkIds = new ulong?[SlotCount];
    private readonly object _lock = new();
    private readonly SemaphoreSlim _storeLock = new(1, 1);
    private bool _initialized;

    public event Action? SlotsChanged;

    public CameraRouterService(IAppDataService appDataService)
    {
        _appDataService = appDataService;
        FrSonic.PortRegistry.Added += OnPortAdded;
        FrSonic.PortRegistry.Deleted += OnPortDeleted;
        FrSonic.LinkRegistry.Added += OnLinkAdded;
        FrSonic.LinkRegistry.Deleted += OnLinkDeleted;
    }

    public IReadOnlyList<CameraSlot> Slots
    {
        get
        {
            lock (_lock)
            {
                return Enumerable.Range(0, SlotCount)
                    .Select(i => new CameraSlot(i, _assignments[i],
                        _linkIds[i] is not null))
                    .ToArray();
            }
        }
    }

    public async Task InitializeAsync()
    {
        var config = await _appDataService.LoadCameraRouterConfig();
        lock (_lock)
        {
            if (_initialized) return;

            foreach (var slot in config?.Slots ?? [])
                if (slot.SlotIndex >= 0 && slot.SlotIndex < SlotCount)
                    _assignments[slot.SlotIndex] = slot.SourceNodeName;

            _initialized = true;
        }

        TryConnectAll();
    }

    public async Task AssignSlotAsync(int slotIndex, string? sourceNodeName)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));

        DisconnectSlot(slotIndex);

        lock (_lock)
            _assignments[slotIndex] =
                string.IsNullOrWhiteSpace(sourceNodeName) ? null : sourceNodeName;

        await StoreConfigAsync();
        SlotsChanged?.Invoke();
        TryConnectSlot(slotIndex);
    }

    public IReadOnlyList<Node> GetCandidateSources()
    {
        // Same shape as MidiRouterService.FindPort/NodeName: resolve a
        // port's owning node via NodeRegistry.GetByObjectId and match by
        // name, not by re-deriving/comparing identity ourselves.
        var outputPortNodeNames = FrSonic.PortRegistry.Objects
            .Where(port => port.Direction == "out")
            .Select(NodeName)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToHashSet();

        return FrSonic.NodeRegistry.Objects
            .Where(node => node.Media.Type == "Video" &&
                           !string.IsNullOrEmpty(node.Name) &&
                           !OwnNodeNames.Contains(node.Name) &&
                           outputPortNodeNames.Contains(node.Name))
            .ToArray();
    }

    private static string? NodeName(Port port) =>
        FrSonic.NodeRegistry.GetByObjectId(port.Node.Id)?.Name;

    private void OnPortAdded(Port port)
    {
        TryConnectAll();
        // TryConnectAll only fires SlotsChanged when a connection actually
        // changes - a newly-visible candidate source with no slot assigned
        // to it wouldn't otherwise trigger a UI refresh, leaving the
        // candidate picker stale until something else happens to change.
        SlotsChanged?.Invoke();
    }

    private void OnPortDeleted(Port port) => SlotsChanged?.Invoke();

    private void OnLinkAdded(Link link)
    {
        var changed = false;
        lock (_lock)
        {
            for (var i = 0; i < SlotCount; ++i)
            {
                if (_linkIds[i] is not null) continue;

                var sourcePort = FindOutputPort(_assignments[i]);
                var targetPort = FindInputPort(CompositorInputName(i));
                if (sourcePort is null || targetPort is null) continue;
                if (!LinkConnects(link, sourcePort, targetPort)) continue;

                _linkIds[i] = link.ObjectId;
                changed = true;
            }
        }

        if (changed) SlotsChanged?.Invoke();
    }

    private void OnLinkDeleted(Link link)
    {
        var changed = false;
        lock (_lock)
        {
            for (var i = 0; i < SlotCount; ++i)
            {
                if (_linkIds[i] != link.ObjectId) continue;
                _linkIds[i] = null;
                changed = true;
            }
        }

        if (!changed) return;
        SlotsChanged?.Invoke();
        // The source node may still be around (only the link died) - retry.
        TryConnectAll();
    }

    private void TryConnectAll()
    {
        for (var i = 0; i < SlotCount; ++i)
            TryConnectSlot(i);
    }

    private void TryConnectSlot(int slotIndex)
    {
        string? sourceNodeName;
        lock (_lock)
        {
            if (_linkIds[slotIndex] is not null) return; // already connected
            sourceNodeName = _assignments[slotIndex];
        }

        if (string.IsNullOrEmpty(sourceNodeName)) return;

        var sourcePort = FindOutputPort(sourceNodeName);
        var targetPort = FindInputPort(CompositorInputName(slotIndex));
        if (sourcePort is null || targetPort is null) return;

        var existingLink = FrSonic.LinkRegistry.Objects.FirstOrDefault(link =>
            LinkConnects(link, sourcePort, targetPort));
        if (existingLink is not null)
        {
            lock (_lock)
                _linkIds[slotIndex] = existingLink.ObjectId;
            SlotsChanged?.Invoke();
            return;
        }

        // Link id becomes known via OnLinkAdded once WirePlumber reports it.
        FrSonic.LinkFactory.CreateLink(sourcePort, targetPort);
    }

    // Same as MidiRouterService's WaitForLinksAsync: Link.OutputPortId/
    // InputPortId are compared directly against Port.ObjectId, no
    // resolution step - this is the proven-working pattern elsewhere.
    private static bool LinkConnects(Link link, Port sourcePort, Port targetPort) =>
        link.OutputPortId == sourcePort.ObjectId &&
        link.InputPortId == targetPort.ObjectId;

    private void DisconnectSlot(int slotIndex)
    {
        ulong? linkId;
        lock (_lock)
        {
            linkId = _linkIds[slotIndex];
            _linkIds[slotIndex] = null;
        }

        if (linkId is null) return;

        var link = FrSonic.LinkRegistry.GetByObjectId(linkId.Value);
        if (link is not null)
            FrSonic.LinkFactory.DeleteLink(link);
    }

    private async Task StoreConfigAsync()
    {
        await _storeLock.WaitAsync();
        try
        {
            List<CameraSlotConfig> slots;
            lock (_lock)
            {
                slots = Enumerable.Range(0, SlotCount)
                    .Where(i => !string.IsNullOrEmpty(_assignments[i]))
                    .Select(i => new CameraSlotConfig
                    {
                        SlotIndex = i,
                        SourceNodeName = _assignments[i]!
                    })
                    .ToList();
            }

            await _appDataService.StoreCameraRouterConfig(new()
            {
                Slots = slots
            });
        }
        finally
        {
            _storeLock.Release();
        }
    }

    private static string CompositorInputName(int slotIndex) =>
        $"se.video-compositor.in{slotIndex}";

    private static Port? FindOutputPort(string? nodeName) =>
        FindPort(nodeName, "out");

    private static Port? FindInputPort(string? nodeName) =>
        FindPort(nodeName, "in");

    // Same shape as MidiRouterService.FindPort: filter ports by direction
    // and by the owning node's name (via NodeName), not by resolving and
    // comparing node identity ourselves.
    private static Port? FindPort(string? nodeName, string direction)
    {
        if (string.IsNullOrEmpty(nodeName)) return null;

        return FrSonic.PortRegistry.Objects.FirstOrDefault(port =>
            port.Direction == direction &&
            string.Equals(NodeName(port), nodeName, StringComparison.Ordinal));
    }

    public void Dispose()
    {
        FrSonic.PortRegistry.Added -= OnPortAdded;
        FrSonic.PortRegistry.Deleted -= OnPortDeleted;
        FrSonic.LinkRegistry.Added -= OnLinkAdded;
        FrSonic.LinkRegistry.Deleted -= OnLinkDeleted;
        _storeLock.Dispose();
    }
}
