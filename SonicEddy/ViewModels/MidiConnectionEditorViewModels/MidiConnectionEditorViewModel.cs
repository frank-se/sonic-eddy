using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Fr.Sonic.Model.Objects;
using ReactiveUI;
using SonicEddy.Controls.GraphEditorControl;
using SonicEddy.ViewModels.MidiConnectionEditorViewModels.Graph;

namespace SonicEddy.ViewModels.MidiConnectionEditorViewModels;

public class MidiConnectionEditorViewModel : ViewModelBase,
    IActivatableViewModel, IRoutableViewModel, IDisposable
{
    public MidiConnectionEditorViewModel(
        List<Port> midiPorts,
        string? urlPathSegment,
        IScreen hostScreen)
    {
        InputPorts = new("Inputs",
            new(midiPorts.Where(p => p.Direction == "out")
                .Select(p => new MidiPortViewModel(p)).OfType<GraphPort>()
                .ToList()));

        OutputPorts = new("Outputs",
            new(midiPorts.Where(p => p.Direction == "in")
                .Select(p => new MidiPortViewModel(p)).OfType<GraphPort>()
                .ToList()));

        UrlPathSegment = urlPathSegment;
        HostScreen = hostScreen;

        Fr.Sonic.FrSonic.LinkRegistry.Added += OnLinkAdded;
        Fr.Sonic.FrSonic.LinkRegistry.Deleted += OnLinkDeleted;
    }

    private void OnLinkAdded(Link link)
    {
        var outPort =
            InputPorts.OutPorts.FirstOrDefault(p =>
            {
                if (p is MidiPortViewModel vm)
                {
                    return vm.Port.ObjectId == link.OutputPortId;
                }

                return false;
            });

        var inPort = OutputPorts.InPorts.FirstOrDefault(p =>
        {
            if (p is MidiPortViewModel vm)
            {
                return vm.Port.ObjectId == link.InputPortId;
            }

            return false;
        });

        if (inPort is null || outPort is null) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Connections.Add(new("Midi Connection", outPort, inPort));
        });
    }

    public MidiInputPortsViewModel InputPorts { get; }

    public MidiOutputPortsViewModel OutputPorts { get; }

    public ObservableCollection<GraphEdge> Connections { get; } = [];

    public ICommand CreateEdgeCommand { get; } =
        ReactiveCommand.Create<(GraphPort, GraphPort)>(edge =>
        {
            if (edge.Item1 is not MidiPortViewModel outPort ||
                edge.Item2 is not MidiPortViewModel inPort) return;

            Fr.Sonic.FrSonic.LinkFactory.CreateLink(outPort.Port,
                inPort.Port);
        });

    private void OnLinkDeleted(Link link)
    {
        var toDelete = Connections.FirstOrDefault(c =>
        {
            if (c.Source is not MidiPortViewModel s ||
                c.Target is not MidiPortViewModel t) return false;

            return s.Port.ObjectId == link.OutputPortId &&
                   t.Port.ObjectId == link.InputPortId;
        });

        if (toDelete is null) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Connections.Remove(toDelete);
        });
    }

    public ViewModelActivator Activator { get; } = new();
    public string? UrlPathSegment { get; }
    public IScreen HostScreen { get; }

    public void Dispose()
    {
        Activator.Dispose();
    }
}