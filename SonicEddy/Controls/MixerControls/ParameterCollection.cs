using System.Collections.Generic;

namespace SonicEddy.Controls.MixerControls;

public record ParameterCollection(
    string Name,
    List<IParameter> Parameters);