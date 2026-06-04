using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using Fr.Sonic;
using Fr.Sonic.Model.Objects;
using ReactiveUI;
using SonicEddy.ViewModels;

namespace SonicEddy.ViewModels.MidiSyncViewModels;

public sealed class MidiSyncViewModel : ViewModelBase
{
    private const string MidiSyncNodeName = "se.midi_sync";
    private bool _refreshing;

    public ObservableCollection<MidiSyncPortViewModel> Ports { get; } = [];

    public string Status
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public MidiSyncViewModel()
    {
        Refresh();
    }

    public void Refresh()
    {
        _refreshing = true;
        try
        {
            var syncOutputPort = FindSyncOutputPort();
            Ports.Clear();

            if (syncOutputPort is null)
            {
                Status = "MIDI sync output is unavailable.";
                return;
            }

            var links = FrSonic.LinkRegistry.Objects
                .Where(link => link.OutputPortId == syncOutputPort.ObjectId)
                .ToList();

            var inputPorts = FrSonic.PortRegistry.Objects
                .Where(IsMidiInputPort)
                .OrderBy(DisplayName);

            foreach (var port in inputPorts)
            {
                var linked = links.Any(link => link.InputPortId == port.ObjectId);
                Ports.Add(new(port, linked, ApplyPort));
            }

            Status = Ports.Count == 0
                ? "No MIDI input ports available."
                : $"{Ports.Count} MIDI input ports available.";
        }
        finally
        {
            _refreshing = false;
        }
    }

    public void Apply()
    {
        foreach (var port in Ports)
            ApplyPort(port);

        Dispatcher.UIThread.Post(Refresh);
    }

    public void SelectAll()
    {
        foreach (var port in Ports)
            port.ReceivesSync = true;
    }

    public void Clear()
    {
        foreach (var port in Ports)
            port.ReceivesSync = false;
    }

    private static Port? FindSyncOutputPort()
    {
        var syncNode = FrSonic.NodeRegistry.Objects
            .FirstOrDefault(node => node.Name == MidiSyncNodeName);
        if (syncNode is null)
            return null;

        return FrSonic.PortRegistry.Objects.FirstOrDefault(port =>
            port.Node.Id == syncNode.ObjectId && port.Direction == "out");
    }

    private static bool IsMidiInputPort(Port port)
    {
        if (port.Direction != "in")
            return false;

        var node = FrSonic.NodeRegistry.GetByObjectId(port.Node.Id);
        return node is { Media.Class: "Midi/Bridge" } &&
               node.Name != MidiSyncNodeName;
    }

    private static string DisplayName(Port port) =>
        port.Alias ?? port.Name ?? $"Port {port.ObjectId}";

    private void ApplyPort(MidiSyncPortViewModel port)
    {
        if (_refreshing || port.ReceivesSync == port.ExistingLink)
            return;

        var syncOutputPort = FindSyncOutputPort();
        if (syncOutputPort is null)
        {
            Status = "MIDI sync output is unavailable.";
            port.ReceivesSync = port.ExistingLink;
            return;
        }

        if (port.ReceivesSync)
            FrSonic.LinkFactory.CreateLink(syncOutputPort, port.Port);
        else
            DeleteExistingLink(syncOutputPort, port.Port);

        port.ExistingLink = port.ReceivesSync;
    }

    private static void DeleteExistingLink(Port syncOutputPort, Port inputPort)
    {
        var link = FrSonic.LinkRegistry.Objects.FirstOrDefault(link =>
            link.OutputPortId == syncOutputPort.ObjectId &&
            link.InputPortId == inputPort.ObjectId);
        if (link is not null)
            FrSonic.LinkFactory.DeleteLink(link);
    }
}
