using System.Collections.Generic;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels;

public record Lv2PluginClass(
    string Uri,
    string Name,
    List<Lv2Plugin> Plugins);