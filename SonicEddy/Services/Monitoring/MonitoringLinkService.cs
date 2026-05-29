using System;
using System.Collections.Generic;
using System.Linq;
using Fr.Sonic;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;
using SonicEddy.Services.MixerServiceV2;

namespace SonicEddy.Services.Monitoring;

public class MonitoringLinkService : IMonitoringLinkService
{
    private Mixer? _mixer;
    private LoopbackModule? _monitoringLoopback;
    private readonly Dictionary<MonitoringChannelKey, MonitoringSource> _selections = new();

    public event Action? StateChanged;

    public void SetMixer(Mixer? mixer)
    {
        _mixer = mixer;
        RecreateAllLinks();
    }

    public void SetMonitoringLoopback(LoopbackModule? loopback)
    {
        _monitoringLoopback = loopback;
        RecreateAllLinks();
    }

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

    private void RecreateAllLinks()
    {
        DeleteAllMonitoringLinks();
        foreach (var (key, source) in _selections)
            CreateLinksForNode(ResolveNode(key, source));
    }

    private void DeleteAllMonitoringLinks()
    {
        var captureNode = _monitoringLoopback?.CaptureNode;
        if (captureNode is null) return;

        foreach (var link in FrSonic.LinkRegistry.Objects
                     .Where(l => l.InputNodeId == captureNode.ObjectId)
                     .ToList())
            FrSonic.LinkFactory.DeleteLink(link);
    }

    private void DeleteLinksForNode(Node? sourceNode)
    {
        if (sourceNode is null) return;
        var captureNode = _monitoringLoopback?.CaptureNode;
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
        var captureNode = _monitoringLoopback?.CaptureNode;
        if (captureNode is null) return;

        var outputPorts = FrSonic.PortRegistry.Objects
            .Where(p => p.Node.Id == sourceNode.ObjectId && p.Direction == "out")
            .ToList();

        var inputPorts = FrSonic.PortRegistry.Objects
            .Where(p => p.Node.Id == captureNode.ObjectId && p.Direction == "in")
            .ToList();

        foreach (var outPort in outputPorts)
        {
            var inPort = inputPorts.FirstOrDefault(ip => ip.Channel == outPort.Channel);
            if (inPort is not null)
                FrSonic.LinkFactory.CreateLink(outPort, inPort);
        }
    }

    private Node? ResolveNode(MonitoringChannelKey key, MonitoringSource source)
    {
        if (source == MonitoringSource.None || _mixer is null) return null;

        var layers = _mixer.Layers;
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
