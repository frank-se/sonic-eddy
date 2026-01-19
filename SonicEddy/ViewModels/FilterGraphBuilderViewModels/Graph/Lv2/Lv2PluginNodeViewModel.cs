using System.Linq;
using Fr.Lv2.Model;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Lv2;

public class Lv2PluginNodeViewModel : NodeViewModelBase
{
    public Lv2PluginNodeViewModel(PluginDescription plugin) : base()
    {
        Plugin = plugin;
        InPorts = plugin.Ports
            .Where(p => p.Classes.Contains(InputPortUri) &&
                        p.Classes.Contains(AudioPortUri))
            .Select(p => new Lv2PortViewModel(p, this))
            .OfType<PortViewModelBase>()
            .ToList();
        OutPorts = plugin.Ports.Where(p => p.Classes.Contains(OutputPortUri) &&
                                           p.Classes.Contains(AudioPortUri))
            .Select(p => new Lv2PortViewModel(p, this))
            .OfType<PortViewModelBase>()
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