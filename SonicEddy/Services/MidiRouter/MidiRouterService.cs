using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fr.Sonic;
using Fr.Sonic.Model.Config.MidiManipulator;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;
using SonicEddy.Contracts.MidiRouter;
using SonicEddy.Services.AppData;

namespace SonicEddy.Services.MidiRouter;

public sealed class MidiRouterService : IMidiRouterService, IDisposable
{
    private readonly IAppDataService _appDataService;
    private readonly List<MidiRoute> _routes = [];
    private readonly List<MidiRouteConfig> _configuredRoutes = [];
    private readonly HashSet<Guid> _routesBeingCreated = [];
    private readonly object _lock = new();
    private readonly SemaphoreSlim _restoreLock = new(1, 1);
    private readonly SemaphoreSlim _storeLock = new(1, 1);
    private bool _initialized;

    public IReadOnlyCollection<MidiRoute> Routes
    {
        get
        {
            lock (_lock)
                return _routes.ToArray();
        }
    }

    public IReadOnlyCollection<MidiRouteConfig> ConfiguredRoutes
    {
        get
        {
            lock (_lock)
                return _configuredRoutes.ToArray();
        }
    }

    public event Action? RoutesChanged;

    public MidiRouterService(IAppDataService appDataService)
    {
        _appDataService = appDataService;
        FrSonic.PortRegistry.Added += OnPortAdded;
    }

    public async Task InitializeAsync()
    {
        var config = await _appDataService.LoadMidiRouterConfig();
        lock (_lock)
        {
            if (_initialized)
                return;

            _configuredRoutes.AddRange(config?.Routes ?? []);
            _initialized = true;
        }

        await ApplyConfiguredRoutesAsync();
    }

    public async Task<MidiRoute> CreateRouteAsync(Port source, Port target,
        MidiManipulationConfig manipulation)
    {
        var routeId = Guid.NewGuid();
        var route = await CreateRouteCoreAsync(routeId, source, target,
            manipulation);

        lock (_lock)
            _configuredRoutes.Add(BuildRouteConfig(route));

        await StoreConfigAsync();
        return route;
    }

    private async Task<MidiRoute> CreateRouteCoreAsync(Guid routeId,
        Port source, Port target, MidiManipulationConfig manipulation)
    {
        if (source.Direction != "out" || target.Direction != "in")
            throw new InvalidOperationException(
                "MIDI routes must connect an output port to an input port.");

        MidiManipulator? manipulator = null;
        List<ulong> linkIds = [];

        try
        {
            if (manipulation.IsPassthrough)
            {
                FrSonic.LinkFactory.CreateLink(source, target);
                linkIds.AddRange(await WaitForLinksAsync(source.ObjectId,
                    target.ObjectId));
            }
            else
            {
                manipulator =
                    await FrSonic.MidiManipulatorFactory
                        .CreateMidiManipulatorAsync(
                            new MidiManipulatorConfig(
                                $"se.midi_router.{routeId:N}",
                                "Sonic Eddy MIDI route manipulator"));
                ApplyManipulatorConfig(manipulator, manipulation);

                var manipulatorInput =
                    await WaitForPortAsync(manipulator.CaptureNode.ObjectId,
                        "in");
                var manipulatorOutput =
                    await WaitForPortAsync(manipulator.PlaybackNode.ObjectId,
                        "out");

                FrSonic.LinkFactory.CreateLink(source, manipulatorInput);
                FrSonic.LinkFactory.CreateLink(manipulatorOutput, target);
                linkIds.AddRange(await WaitForLinksAsync(source.ObjectId,
                    manipulatorInput.ObjectId));
                linkIds.AddRange(await WaitForLinksAsync(
                    manipulatorOutput.ObjectId,
                    target.ObjectId));
            }
        }
        catch
        {
            foreach (var linkId in linkIds)
            {
                var link = FrSonic.LinkRegistry.GetByObjectId(linkId);
                if (link is not null)
                    FrSonic.LinkFactory.DeleteLink(link);
            }

            manipulator?.Destroy();
            throw;
        }

        var route = new MidiRoute(routeId, source, target, manipulation,
            manipulator, linkIds);
        lock (_lock)
            _routes.Add(route);
        RoutesChanged?.Invoke();
        return route;
    }

    public void DeleteRoute(Guid routeId)
    {
        MidiRoute? route;
        lock (_lock)
            route = _routes.FirstOrDefault(route => route.Id == routeId);
        if (route is null)
            return;

        foreach (var linkId in route.OwnedLinkIds)
        {
            var link = FrSonic.LinkRegistry.GetByObjectId(linkId);
            if (link is not null)
                FrSonic.LinkFactory.DeleteLink(link);
        }

        route.Manipulator?.Destroy();
        lock (_lock)
        {
            _routes.Remove(route);
            _configuredRoutes.RemoveAll(config => config.Id == routeId);
        }

        _ = StoreConfigAsync();
        RoutesChanged?.Invoke();
    }

    private async Task ApplyConfiguredRoutesAsync()
    {
        await _restoreLock.WaitAsync();
        try
        {
            MidiRouteConfig[] pending;
            lock (_lock)
            {
                pending = _configuredRoutes
                    .Where(config =>
                        _routes.All(route => route.Id != config.Id) &&
                        _routesBeingCreated.Add(config.Id))
                    .ToArray();
            }

            foreach (var config in pending)
            {
                try
                {
                    var source = FindPort(config.Source);
                    var target = FindPort(config.Target);
                    if (source is null || target is null)
                        continue;

                    var manipulation = new MidiManipulationConfig(
                        config.DropChannels.ToArray(),
                        config.ChannelMap
                            .Select(entry =>
                                new ChannelMapEntry(entry.From, entry.To))
                            .ToArray());
                    await CreateRouteCoreAsync(config.Id, source, target,
                        manipulation);
                }
                catch
                {
                    // Keep the persisted route so it can be retried when the
                    // PipeWire MIDI graph changes.
                }
                finally
                {
                    lock (_lock)
                        _routesBeingCreated.Remove(config.Id);
                }
            }
        }
        finally
        {
            _restoreLock.Release();
        }
    }

    private async Task StoreConfigAsync()
    {
        await _storeLock.WaitAsync();
        try
        {
            MidiRouteConfig[] routes;
            lock (_lock)
                routes = _configuredRoutes.ToArray();

            await _appDataService.StoreMidiRouterConfig(new()
            {
                Routes = routes.ToList()
            });
        }
        finally
        {
            _storeLock.Release();
        }
    }

    private void OnPortAdded(Port port) =>
        _ = ApplyConfiguredRoutesAsync();

    private static Port? FindPort(MidiRoutePortConfig config)
    {
        var candidates = FrSonic.PortRegistry.Objects.Where(port =>
            port.Direction == config.Direction &&
            string.Equals(NodeName(port), config.NodeName,
                StringComparison.Ordinal));

        if (!string.IsNullOrEmpty(config.PortAlias))
        {
            var byAlias = candidates.FirstOrDefault(port =>
                string.Equals(port.Alias, config.PortAlias,
                    StringComparison.Ordinal));
            if (byAlias is not null)
                return byAlias;
        }

        return candidates.FirstOrDefault(port =>
            string.Equals(port.Name, config.PortName,
                StringComparison.Ordinal));
    }

    private static MidiRouteConfig BuildRouteConfig(MidiRoute route) => new()
    {
        Id = route.Id,
        Source = BuildPortConfig(route.Source),
        Target = BuildPortConfig(route.Target),
        DropChannels = route.Manipulation.DropChannels.ToList(),
        ChannelMap = route.Manipulation.ChannelMap
            .Select(entry => new MidiChannelMapConfig
            {
                From = entry.From,
                To = entry.To
            })
            .ToList()
    };

    private static MidiRoutePortConfig BuildPortConfig(Port port) => new()
    {
        NodeName = NodeName(port),
        PortName = port.Name ?? string.Empty,
        PortAlias = port.Alias ?? string.Empty,
        Direction = port.Direction ?? string.Empty
    };

    private static string NodeName(Port port) =>
        FrSonic.NodeRegistry.GetByObjectId(port.Node.Id)?.Name ?? string.Empty;

    private static void ApplyManipulatorConfig(MidiManipulator manipulator,
        MidiManipulationConfig manipulation)
    {
        var payload = new
        {
            version = 1,
            drop_channels = manipulation.DropChannels,
            channel_map = manipulation.ChannelMap
                .Select(entry => new[] { entry.From, entry.To })
                .ToArray()
        };
        manipulator.CaptureNode.SetParam("midi.router.config",
            JsonSerializer.Serialize(payload));
    }

    private static async Task<Port> WaitForPortAsync(ulong nodeObjectId,
        string direction)
    {
        for (var attempt = 0; attempt < 50; ++attempt)
        {
            var port = FrSonic.PortRegistry.Objects.FirstOrDefault(port =>
                port.Node.Id == nodeObjectId && port.Direction == direction);
            if (port is not null)
                return port;

            await Task.Delay(20);
        }

        throw new InvalidOperationException(
            $"Could not resolve MIDI manipulator {direction} port.");
    }

    private static async Task<IReadOnlyList<ulong>> WaitForLinksAsync(
        ulong sourcePortId,
        ulong targetPortId)
    {
        for (var attempt = 0; attempt < 50; ++attempt)
        {
            var links = FrSonic.LinkRegistry.Objects
                .Where(link => link.OutputPortId == sourcePortId &&
                               link.InputPortId == targetPortId)
                .Select(link => link.ObjectId)
                .ToList();
            if (links.Count > 0)
                return links;

            await Task.Delay(20);
        }

        throw new InvalidOperationException(
            $"Could not resolve MIDI route link {sourcePortId}->{targetPortId}.");
    }

    public void Dispose()
    {
        FrSonic.PortRegistry.Added -= OnPortAdded;
        _restoreLock.Dispose();
        _storeLock.Dispose();
    }
}
