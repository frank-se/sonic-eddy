using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Fr.Lv2.Model;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels;

public class Lv2PluginNode : NodeBase
{
    public Lv2PluginNode(PluginDescription plugin) : base()
    {
        Plugin = plugin;
        InPorts = plugin.Ports
            .Where(p => p.Classes.Contains(InputPortUri) &&
                        p.Classes.Contains(AudioPortUri))
            .Select(p => new Lv2PortNode(p, this)).OfType<PortNodeBase>()
            .ToList();
        OutPorts = plugin.Ports.Where(p => p.Classes.Contains(OutputPortUri) &&
                                           p.Classes.Contains(AudioPortUri))
            .Select(p => new Lv2PortNode(p, this)).OfType<PortNodeBase>()
            .ToList();
    }

    private const string OutputPortUri =
        "http://lv2plug.in/ns/lv2core#OutputPort";

    private const string
        InputPortUri = "http://lv2plug.in/ns/lv2core#InputPort";

    private const string
        AudioPortUri = "http://lv2plug.in/ns/lv2core#AudioPort";

    public PluginDescription Plugin { get; }
}