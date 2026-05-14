using Fr.Wireplumber.Model.Objects;

namespace Fr.Wireplumber.Registries.Ports;

/// <summary>
/// Provides access to pipewire ports
/// </summary>
public class
    PortRegistry : Registry<Port, PortTaskCompletionSources, PortChangeType>
{
}