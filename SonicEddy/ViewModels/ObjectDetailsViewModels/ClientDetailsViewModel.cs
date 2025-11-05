using Fr.Wireplumber.Model;
using ReactiveUI;

namespace SonicEddy.ViewModels.ObjectDetailsViewModels;

public class ClientDetailsViewModel(
    ulong objectId,
    ulong objectSerial,
    string applicationName,
    ulong processId,
    ulong groupId,
    ulong userId,
    string socket,
    string access,
    string clientAccess,
    string protocol)
    : ObjectDetailsViewModelBase(objectId, objectSerial, "Client"),
        IActivatableViewModel
{
    public string ApplicationName => applicationName;
    public ulong ProcessId => processId;
    public ulong GroupId => groupId;
    public ulong UserId => userId;
    public string Socket => socket;
    public string Access => access;
    public string ClientAccess => clientAccess;
    public string Protocol => protocol;
    
    public ViewModelActivator Activator { get; } = new();

    public static ClientDetailsViewModel FromClient(Client client)
    {
        return new(client.ObjectId, client.ObjectSerial,
            client.Application.Name ?? string.Empty,
            client.Pipewire.Security.Pid,
            client.Pipewire.Security.Gid,
            client.Pipewire.Security.Uid,
            client.Pipewire.Security.Socket ?? string.Empty,
            client.Pipewire.Access ?? string.Empty,
            client.Pipewire.ClientAccess ?? string.Empty,
            client.Pipewire.Protocol ?? string.Empty);
    }
}