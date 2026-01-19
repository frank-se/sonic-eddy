using System;
using System.Collections.Generic;
using System.Linq;
using Fr.Wireplumber.Model.Config.FilterChain;
using SonicEddy.Contracts.FilterGraph;

namespace SonicEddy.Conversions;

public static class GrpcToFrWireplumberExtensions
{
    public static FilterGraphConfig ToFilterGraphConfig(
        this FilterGraph filterGraph)
    {
        var portToNodeMapping = BuildPortToNodeMapping(filterGraph);

        List<FilterGraphLink> links = [];
        List<string> inputs = [];
        List<string> outputs = [];
        foreach (var edge in filterGraph.Edges)
        {
            var outputNode = portToNodeMapping[edge.Source];
            var inputNode = portToNodeMapping[edge.Target];
            switch (outputNode, inputNode)
            {
                case (FilterGraphLv2Plugin o, FilterGraphLv2Plugin i):
                {
                    var outPort = o.OutputPorts.First(p => p.Id == edge.Source);
                    var inPort = i.InputPorts.First(p => p.Id == edge.Target);
                    links.Add(new()
                    {
                        Output = $"{outputNode.Name}:{outPort.Symbol}",
                        Input = $"{inputNode.Name}:{inPort.Symbol}"
                    });
                }
                    break;
                case (FilterGraphLv2Plugin o, FilterGraphOutput i):
                {
                    var outPort = o.OutputPorts.First(p => p.Id == edge.Source);
                    outputs.Add($"{o.Name}:{outPort.Symbol}");
                }
                    break;
                case (FilterGraphInput o, FilterGraphLv2Plugin i):
                {
                    var inPort = i.InputPorts.First(p => p.Id == edge.Target);
                    inputs.Add($"{i.Name}:{inPort.Symbol}");
                }
                    break;
            }
        }

        var nodes =
            filterGraph.Nodes.OfType<FilterGraphLv2Plugin>().Select(n =>
                new FilterGraphNode()
                {
                    Name = n.Name,
                    Plugin = n.Uri,
                    Type = "lv2"
                }).ToList();

        return new()
        {
            Links = links,
            Nodes = nodes,
            Inputs = inputs,
            Outputs = outputs
        };
    }

    private static Dictionary<Guid, FilterGraphNodeBase> BuildPortToNodeMapping(
        FilterGraph filterGraph)
    {
        Dictionary<Guid, FilterGraphNodeBase> portToNodeMapping = [];
        foreach (var node in filterGraph.Nodes)
        {
            var portIds = node switch
            {
                FilterGraphLv2Plugin n => n.InputPorts.Select(p => p.Id)
                    .Concat(n.OutputPorts.Select(p => p.Id)).ToList(),
                FilterGraphOutput n => n.InputPorts.Select(p => p.Id).ToList(),
                FilterGraphInput n => n.OutputPorts.Select(p => p.Id).ToList(),
                _ => throw new NotImplementedException()
            };

            foreach (var id in portIds)
            {
                portToNodeMapping.Add(id, node);
            }
        }

        return portToNodeMapping;
    }
}