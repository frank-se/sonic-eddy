using System;
using System.Collections.Generic;
using System.Linq;
using Fr.Sonic;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;
using SonicEddy.Services.MixerServiceV2;

namespace SonicEddy.Services.Monitoring;

public class MonitoringLinkService(IMixerService mixerService) : IMonitoringLinkService
{
    private readonly Dictionary<MonitoringChannelKey, MonitoringSource> _selections = new();

    public event Action? StateChanged;

    private Mixer? Mixer => mixerService.CurrentMixer;
    private LoopbackModule? MonitoringLoopback => Mixer?.MonitoringLoopback;

    public void SetSource(MonitoringChannelKey key, MonitoringSource source)
    {
        var current = GetSource(key);
        if (current == source) return;

        DeleteLinksForNode(ResolveNode(key, current));

        _selections[key] = source;

        CreateLinksForNode(ResolveNode(key, source));

        StateChanged?.Invoke();
    }

    public MonitoringSource GetSource(MonitoringChannelKey key) =>
        _selections.GetValueOrDefault(key, MonitoringSource.None);

    private void DeleteLinksForNode(Node? sourceNode)
    {
        if (sourceNode is null) return;
        var captureNode = MonitoringLoopback?.CaptureNode;
        if (captureNode is null) return;

        foreach (var link in FrSonic.LinkRegistry.Objects
                     .Where(l => l.OutputNodeId == sourceNode.ObjectId &&
                                 l.InputNodeId == captureNode.ObjectId)
                     .ToList())
            FrSonic.LinkFactory.DeleteLink(link);
    }

    private void CreateLinksForNode(Node? sourceNode)
    {
        if (sourceNode is null) return;
        var captureNode = MonitoringLoopback?.CaptureNode;
        if (captureNode is null) return;

        var outputPorts = FrSonic.PortRegistry.Objects
            .Where(p => p.Node.Id == sourceNode.ObjectId && p.Direction == "out")
            .OrderBy(p => p.PortId)
            .ToList();

        var inputPorts = FrSonic.PortRegistry.Objects
            .Where(p => p.Node.Id == captureNode.ObjectId && p.Direction == "in")
            .OrderBy(p => p.PortId)
            .ToList();

        var count = Math.Min(outputPorts.Count, inputPorts.Count);
        for (var i = 0; i < count; i++)
            FrSonic.LinkFactory.CreateLink(outputPorts[i], inputPorts[i]);
    }

    public Node? ResolvePickUpNode(MonitoringChannelKey key, MonitoringSource source) =>
        ResolveNode(key, source);

    private Node? ResolveNode(MonitoringChannelKey key, MonitoringSource source)
    {
        if (source == MonitoringSource.None || Mixer is null) return null;

        var layers = Mixer.Layers;
        if (key.LayerIndex >= layers.Length) return null;
        var layer = layers[key.LayerIndex];

        return key.ChannelType switch
        {
            MonitoringChannelType.Strip when key.ChannelIndex < layer.Channels.Count =>
                ResolveStripNode(
                    layer.Channels[key.ChannelIndex].InputLoopback,
                    layer.Channels[key.ChannelIndex].PreFxLooper,
                    layer.Channels[key.ChannelIndex].FilterChain,
                    layer.Channels[key.ChannelIndex].PostFxLooper,
                    source),

            MonitoringChannelType.Group when key.ChannelIndex < layer.GroupChannels.Count =>
                ResolveStripNode(
                    layer.GroupChannels[key.ChannelIndex].InputLoopback,
                    layer.GroupChannels[key.ChannelIndex].PreFxLooper,
                    layer.GroupChannels[key.ChannelIndex].FilterChain,
                    layer.GroupChannels[key.ChannelIndex].PostFxLooper,
                    source),

            MonitoringChannelType.Master =>
                ResolveStripNode(
                    layer.MasterChannel.InputLoopback,
                    layer.MasterChannel.PreFxLooper,
                    layer.MasterChannel.FilterChain,
                    layer.MasterChannel.PostFxLooper,
                    source),

            MonitoringChannelType.Return when key.ChannelIndex < layer.SendReturns.Count =>
                ResolveReturnNode(layer.SendReturns[key.ChannelIndex], source),

            _ => null
        };
    }

    private static Node? ResolveStripNode(
        TwoNodePipewireModule inputLoopback,
        Looper preFxLooper,
        FilterChain? filterChain,
        Looper postFxLooper,
        MonitoringSource source) => source switch
    {
        MonitoringSource.Pre         => inputLoopback.CaptureNode,
        MonitoringSource.Post        => preFxLooper.CaptureNode,
        MonitoringSource.OutPreFader  => filterChain?.CaptureNode ?? preFxLooper.CaptureNode,
        MonitoringSource.OutPostFader => postFxLooper.CaptureNode,
        _                            => null
    };

    private static Node? ResolveReturnNode(ReturnChannel ret, MonitoringSource source) =>
        source switch
        {
            MonitoringSource.Pre         => ret.InputLoopback.CaptureNode,
            MonitoringSource.Post        => ret.InputLoopback.CaptureNode,
            MonitoringSource.OutPreFader  => ret.FilterChain?.CaptureNode ?? ret.InputLoopback.CaptureNode,
            MonitoringSource.OutPostFader => ret.OutputLoopback.CaptureNode,
            _                            => null
        };
}
