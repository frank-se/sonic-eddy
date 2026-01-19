using System.Collections.Generic;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Toolbox;

public record Lv2PluginClass(
    string Uri,
    string Name,
    List<AvailableLv2Plugin> Plugins);