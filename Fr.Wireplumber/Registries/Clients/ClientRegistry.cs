using Fr.Wireplumber.Model.Objects;

namespace Fr.Wireplumber.Registries.Clients;

/// <summary>
/// Get access to pipewire clients.
/// </summary>
public class ClientRegistry : Registry<Client, ClientTaskCompletionSources,
    ClientChangeType>
{
}