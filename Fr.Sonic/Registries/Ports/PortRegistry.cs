using Fr.Sonic.Model.Objects;

namespace Fr.Sonic.Registries.Ports;

/// <summary>
/// Provides access to pipewire ports
/// </summary>
public class
    PortRegistry : Registry<Port, PortTaskCompletionSources, PortChangeType>
{
}