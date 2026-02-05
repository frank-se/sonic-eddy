using System.Linq;
using Fr.Lv2.Model;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Lv2;

public class Lv2PluginNodeViewModel(PluginDescription plugin)
    : NodeViewModelBase(plugin.Name,
        new(plugin.Ports
            .Where(p => p.Classes.Contains(InputPortUri) &&
                        p.Classes.Contains(AudioPortUri))
            .Select(p => new Lv2PortViewModel(p))
            .OfType<PortViewModelBase>()
            .ToList()), new(plugin.Ports.Where(p =>
                p.Classes.Contains(OutputPortUri) &&
                p.Classes.Contains(AudioPortUri))
            .Select(p => new Lv2PortViewModel(p))
            .OfType<PortViewModelBase>()
            .ToList()))
{
    private const string OutputPortUri =
        "http://lv2plug.in/ns/lv2core#OutputPort";

    private const string
        InputPortUri = "http://lv2plug.in/ns/lv2core#InputPort";

    private const string
        AudioPortUri = "http://lv2plug.in/ns/lv2core#AudioPort";

    public PluginDescription Plugin { get; } = plugin;
}