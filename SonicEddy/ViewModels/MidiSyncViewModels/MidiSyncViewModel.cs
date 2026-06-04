using System.Collections.ObjectModel;
using Avalonia.Threading;
using ReactiveUI;
using SonicEddy.Services.MidiSync;
using SonicEddy.ViewModels;
using Splat;

namespace SonicEddy.ViewModels.MidiSyncViewModels;

public sealed class MidiSyncViewModel : ViewModelBase
{
    private readonly IMidiSyncLinkService? _midiSyncLinkService;
    private bool _refreshing;

    public ObservableCollection<MidiSyncPortViewModel> Ports { get; } = [];

    public string Status
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public MidiSyncViewModel()
        : this(Locator.Current.GetService<IMidiSyncLinkService>())
    {
    }

    public MidiSyncViewModel(IMidiSyncLinkService? midiSyncLinkService)
    {
        _midiSyncLinkService = midiSyncLinkService;
        if (_midiSyncLinkService is not null)
            _midiSyncLinkService.Changed += OnMidiSyncLinksChanged;
        Refresh();
    }

    public void Refresh()
    {
        _refreshing = true;
        try
        {
            Ports.Clear();

            if (_midiSyncLinkService is null)
            {
                Status = "MIDI sync output is unavailable.";
                return;
            }

            foreach (var port in _midiSyncLinkService.GetPorts())
                Ports.Add(new(port.Port, port.ReceivesSync,
                    port.ExistingLink, ApplyPort));

            Status = !_midiSyncLinkService.IsSyncOutputAvailable
                ? "MIDI sync output is unavailable."
                : Ports.Count == 0
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

    private void ApplyPort(MidiSyncPortViewModel port)
    {
        if (_refreshing)
            return;

        _midiSyncLinkService?.SetReceivesSync(port.Port, port.ReceivesSync);
        port.ExistingLink = port.ReceivesSync;
    }

    private void OnMidiSyncLinksChanged() =>
        Dispatcher.UIThread.Post(Refresh);
}
