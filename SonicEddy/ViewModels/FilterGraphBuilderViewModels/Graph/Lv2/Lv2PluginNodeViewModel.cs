using System.Collections.ObjectModel;
using System.Linq;
using Fr.Sonic.Model.Lv2;

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
    private const string OutputPortUri = Lv2PortUris.OutputPortUri;
    private const string InputPortUri  = Lv2PortUris.InputPortUri;
    private const string AudioPortUri  = Lv2PortUris.AudioPortUri;

    public PluginDescription Plugin { get; } = plugin;

    public ReadOnlyCollection<Lv2ControlViewModel> Controls { get; } =
        new(plugin.Ports
            .Where(p => p.Classes.Contains(Lv2PortUris.ControlPortUri) &&
                        p.Classes.Contains(Lv2PortUris.InputPortUri))
            .Select(p => new Lv2ControlViewModel(p))
            .ToList());
}