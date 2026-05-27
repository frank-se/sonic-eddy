using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Fr.Sonic;
using Fr.Sonic.Model.Config.MidiManipulator;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;

namespace SonicEddy.Services.MidiRouter;

public sealed class MidiRouterService : IMidiRouterService
{
    private readonly List<MidiRoute> _routes = [];

    public IReadOnlyCollection<MidiRoute> Routes => _routes;
    public event Action? RoutesChanged;

    public async Task<MidiRoute> CreateRouteAsync(Port source, Port target,
        MidiManipulationConfig manipulation)
    {
        if (source.Direction != "out" || target.Direction != "in")
            throw new InvalidOperationException(
                "MIDI routes must connect an output port to an input port.");

        var routeId = Guid.NewGuid();
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
        _routes.Add(route);
        RoutesChanged?.Invoke();
        return route;
    }

    public void DeleteRoute(Guid routeId)
    {
        var route = _routes.FirstOrDefault(route => route.Id == routeId);
        if (route is null)
            return;

        foreach (var linkId in route.OwnedLinkIds)
        {
            var link = FrSonic.LinkRegistry.GetByObjectId(linkId);
            if (link is not null)
                FrSonic.LinkFactory.DeleteLink(link);
        }

        route.Manipulator?.Destroy();
        _routes.Remove(route);
        RoutesChanged?.Invoke();
    }

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
}
