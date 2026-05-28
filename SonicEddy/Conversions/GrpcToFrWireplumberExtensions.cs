using System;
using System.Collections.Generic;
using System.Linq;
using Fr.Sonic.Model.Config.FilterChain;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Toolbox;

namespace SonicEddy.Conversions;

public static class GrpcToFrWireplumberExtensions
{
    public static FilterGraphConfig ToFilterGraphConfig(this FilterGraph filterGraph)
    {
        var portToNode = BuildPortToNodeMapping(filterGraph);

        List<FilterGraphLink> links = [];
        List<string> inputs = [];
        List<string> outputs = [];

        foreach (var edge in filterGraph.Edges)
        {
            var outNode = portToNode[edge.Source];
            var inNode  = portToNode[edge.Target];

            switch (outNode, inNode)
            {
                /* lv2 → lv2 */
                case (FilterGraphLv2Plugin o, FilterGraphLv2Plugin i):
                    links.Add(Lv2ToLv2Link(o, i, edge));
                    break;

                /* builtin → builtin */
                case (FilterGraphBuiltinNode o, FilterGraphBuiltinNode i):
                    links.Add(BuiltinToBuiltinLink(o, i, edge));
                    break;

                /* lv2 → builtin */
                case (FilterGraphLv2Plugin o, FilterGraphBuiltinNode i):
                {
                    var outPort = o.OutputPorts.First(p => p.Id == edge.Source);
                    var inPort  = i.InputPorts.First(p => p.Id == edge.Target);
                    links.Add(new() { Output = $"{o.Name}:{outPort.Symbol}", Input = $"{i.Name}:{inPort.Name}" });
                    break;
                }

                /* builtin → lv2 */
                case (FilterGraphBuiltinNode o, FilterGraphLv2Plugin i):
                {
                    var outPort = o.OutputPorts.First(p => p.Id == edge.Source);
                    var inPort  = i.InputPorts.First(p => p.Id == edge.Target);
                    links.Add(new() { Output = $"{o.Name}:{outPort.Name}", Input = $"{i.Name}:{inPort.Symbol}" });
                    break;
                }

                /* lv2 → graph output */
                case (FilterGraphLv2Plugin o, FilterGraphOutput):
                {
                    var outPort = o.OutputPorts.First(p => p.Id == edge.Source);
                    outputs.Add($"{o.Name}:{outPort.Symbol}");
                    break;
                }

                /* builtin → graph output */
                case (FilterGraphBuiltinNode o, FilterGraphOutput):
                {
                    var outPort = o.OutputPorts.First(p => p.Id == edge.Source);
                    outputs.Add($"{o.Name}:{outPort.Name}");
                    break;
                }

                /* graph input → lv2 */
                case (FilterGraphInput, FilterGraphLv2Plugin i):
                {
                    var inPort = i.InputPorts.First(p => p.Id == edge.Target);
                    inputs.Add($"{i.Name}:{inPort.Symbol}");
                    break;
                }

                /* graph input → builtin */
                case (FilterGraphInput, FilterGraphBuiltinNode i):
                {
                    var inPort = i.InputPorts.First(p => p.Id == edge.Target);
                    inputs.Add($"{i.Name}:{inPort.Name}");
                    break;
                }
            }
        }

        var lv2Nodes = filterGraph.Nodes.OfType<FilterGraphLv2Plugin>()
            .Select(n => new FilterGraphNode
            {
                Name   = n.Name,
                Type   = "lv2",
                Plugin = n.Uri,
                Control = n.InitialControls.Count > 0
                    ? n.InitialControls.ToDictionary(c => c.Symbol, c => (object)(double)c.Value)
                    : null
            });

        var builtinNodes = filterGraph.Nodes.OfType<FilterGraphBuiltinNode>()
            .Select(n => new FilterGraphNode
            {
                Name   = n.Name,
                Type   = "builtin",
                Plugin = n.NodeType.ToPwName(),
                Control = n.InitialControls.Count > 0
                    ? n.InitialControls.ToDictionary(c => c.Name, c => (object)c.Value)
                    : null
            });

        return new()
        {
            Nodes   = lv2Nodes.Concat(builtinNodes).ToList(),
            Links   = links,
            Inputs  = inputs,
            Outputs = outputs
        };
    }

    private static FilterGraphLink Lv2ToLv2Link(
        FilterGraphLv2Plugin o, FilterGraphLv2Plugin i, FilterGraphEdge edge)
    {
        var outPort = o.OutputPorts.First(p => p.Id == edge.Source);
        var inPort  = i.InputPorts.First(p => p.Id == edge.Target);
        return new() { Output = $"{o.Name}:{outPort.Symbol}", Input = $"{i.Name}:{inPort.Symbol}" };
    }

    private static FilterGraphLink BuiltinToBuiltinLink(
        FilterGraphBuiltinNode o, FilterGraphBuiltinNode i, FilterGraphEdge edge)
    {
        var outPort = o.OutputPorts.First(p => p.Id == edge.Source);
        var inPort  = i.InputPorts.First(p => p.Id == edge.Target);
        return new() { Output = $"{o.Name}:{outPort.Name}", Input = $"{i.Name}:{inPort.Name}" };
    }

    private static Dictionary<Guid, FilterGraphNodeBase> BuildPortToNodeMapping(
        FilterGraph filterGraph)
    {
        Dictionary<Guid, FilterGraphNodeBase> map = [];
        foreach (var node in filterGraph.Nodes)
        {
            var ids = node switch
            {
                FilterGraphLv2Plugin n =>
                    n.InputPorts.Select(p => p.Id).Concat(n.OutputPorts.Select(p => p.Id)),
                FilterGraphBuiltinNode n =>
                    n.InputPorts.Select(p => p.Id).Concat(n.OutputPorts.Select(p => p.Id)),
                FilterGraphOutput n =>
                    n.InputPorts.Select(p => p.Id),
                FilterGraphInput n =>
                    n.OutputPorts.Select(p => p.Id),
                _ => throw new NotImplementedException()
            };
            foreach (var id in ids) map.Add(id, node);
        }
        return map;
    }
}
