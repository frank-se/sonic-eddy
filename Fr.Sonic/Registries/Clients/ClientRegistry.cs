using Fr.Sonic.Model.Objects;

namespace Fr.Sonic.Registries.Clients;

/// <summary>
/// Get access to pipewire clients.
/// </summary>
public class ClientRegistry : Registry<Client, ClientTaskCompletionSources,
    ClientChangeType>
{
}