using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Fr.Wireplumber.Model.Objects;
using ReactiveUI;
using SonicEddy.Controls.GraphEditorControl;
using SonicEddy.ViewModels.MidiConnectionEditorViewModels.Graph;

namespace SonicEddy.ViewModels.MidiConnectionEditorViewModels;

public class MidiConnectionEditorViewModel(
    List<Port> midiPorts,
    string? urlPathSegment,
    IScreen hostScreen)
    : ViewModelBase, IActivatableViewModel, IRoutableViewModel
{
    public MidiInputPortsViewModel InputPorts { get; } = new("Inputs",
        new(midiPorts.Where(p => p.Direction == "out")
            .Select(p => new MidiPortViewModel(p)).OfType<GraphPort>()
            .ToList()));

    public MidiOutputPortsViewModel OutputPorts { get; } = new("Outputs",
        new(midiPorts.Where(p => p.Direction == "in")
            .Select(p => new MidiPortViewModel(p)).OfType<GraphPort>()
            .ToList()));

    public ObservableCollection<GraphConnection> Connections { get; } = [];

    public ViewModelActivator Activator { get; } = new();
    public string? UrlPathSegment { get; } = urlPathSegment;
    public IScreen HostScreen { get; } = hostScreen;
}