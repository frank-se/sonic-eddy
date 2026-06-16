using System;
using System.Linq;
using Fr.Sonic;
using Fr.Sonic.Model.Objects;
using ReactiveUI;
using SonicEddy.Services.JackInputPorts;
using SonicEddy.Services.Wireplumber;

namespace SonicEddy.ViewModels.JackInputPortsViewModels;

public class JackInputPortViewModel : ReactiveObject, IDisposable
{
    private readonly IWireplumberService _wireplumberService;

    public JackInputPortViewModel(JackInputPort port,
        IWireplumberService wireplumberService)
    {
        Port = port;
        JackPorts = string.Join(", ", port.JackPortNames);
        _wireplumberService = wireplumberService;
        _wireplumberService.NodeAdded += OnNodeChanged;
        _wireplumberService.NodeDeleted += OnNodeChanged;
        UpdateConnected();
    }

    public JackInputPort Port { get; }
    public string JackPorts { get; }

    public bool IsConnected
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    private void OnNodeChanged(Node _) => UpdateConnected();

    private void UpdateConnected()
    {
        IsConnected = FrSonic.NodeRegistry.Objects.Any(n =>
            n.Client?.Api == "jack" &&
            string.Equals(n.Name, Port.ClientName, StringComparison.Ordinal));
    }

    public void Dispose()
    {
        _wireplumberService.NodeAdded -= OnNodeChanged;
        _wireplumberService.NodeDeleted -= OnNodeChanged;
        GC.SuppressFinalize(this);
    }
}
