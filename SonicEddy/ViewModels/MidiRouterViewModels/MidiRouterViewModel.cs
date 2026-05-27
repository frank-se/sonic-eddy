using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Threading;
using Fr.Sonic;
using Fr.Sonic.Model.Objects;
using ReactiveUI;
using SonicEddy.Controls.GraphEditorControl;

namespace SonicEddy.ViewModels.MidiRouterViewModels;

public sealed class MidiRouterViewModel : ViewModelBase, IDisposable
{
    private readonly Dictionary<ulong, MidiRouterPortViewModel> _ports = [];

    public MidiRouterViewModel()
    {
        CreateEdgeCommand =
            ReactiveCommand.Create<(GraphPort Source, GraphPort Target)>(
                CreateRoute);
        RefreshCommand = ReactiveCommand.Create(Refresh);

        FrSonic.PortRegistry.Added += OnPortChanged;
        FrSonic.PortRegistry.Deleted += OnPortChanged;
        FrSonic.LinkRegistry.Added += OnLinkChanged;
        FrSonic.LinkRegistry.Deleted += OnLinkChanged;

        Refresh();
    }

    public GraphNode? Sources
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public GraphNode? Targets
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ObservableCollection<GraphEdge> Routes { get; } = [];
    public ObservableCollection<MidiRouteLinkViewModel> RouteLinks { get; } = [];

    public ICommand CreateEdgeCommand { get; }
    public ICommand RefreshCommand { get; }

    public string Status
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public void Refresh()
    {
        _ports.Clear();
        Routes.Clear();
        RouteLinks.Clear();

        var controlPorts = FrSonic.PortRegistry.Objects
            .Where(IsControlPort)
            .OrderBy(DisplayName)
            .ToList();

        foreach (var port in controlPorts)
            _ports[port.ObjectId] = new(port, DisplayName(port));

        Sources = new("Sources",
            new([]),
            new(_ports.Values
                .Where(port => port.Port.Direction == "out")
                .OfType<GraphPort>()
                .ToList()));
        Targets = new("Targets",
            new(_ports.Values
                .Where(port => port.Port.Direction == "in")
                .OfType<GraphPort>()
                .ToList()),
            new([]));

        foreach (var link in FrSonic.LinkRegistry.Objects
                     .Where(IsVisibleRoute)
                     .OrderBy(link => DisplayName(PortById(link.OutputPortId)))
                     .ThenBy(link => DisplayName(PortById(link.InputPortId))))
            AddRoute(link);

        Status =
            $"{Sources.OutPorts.Count} source ports, {Targets.InPorts.Count} target ports, {Routes.Count} routes.";
    }

    private void CreateRoute((GraphPort Source, GraphPort Target) edge)
    {
        if (edge.Source is not MidiRouterPortViewModel source ||
            edge.Target is not MidiRouterPortViewModel target)
            return;

        if (source.Port.Direction != "out" || target.Port.Direction != "in")
            return;

        var existing = FrSonic.LinkRegistry.Objects.Any(link =>
            link.OutputPortId == source.Port.ObjectId &&
            link.InputPortId == target.Port.ObjectId);
        if (existing)
            return;

        FrSonic.LinkFactory.CreateLink(source.Port, target.Port);
    }

    private void DeleteRoute(Link link)
    {
        FrSonic.LinkFactory.DeleteLink(link);
    }

    private void AddRoute(Link link)
    {
        if (!_ports.TryGetValue(link.OutputPortId, out var source) ||
            !_ports.TryGetValue(link.InputPortId, out var target))
            return;

        Routes.Add(new MidiRouterEdgeViewModel("MIDI", source, target, link));
        RouteLinks.Add(new(link, DisplayName(source.Port),
            DisplayName(target.Port), DeleteRoute));
    }

    private bool IsVisibleRoute(Link link) =>
        _ports.ContainsKey(link.OutputPortId) &&
        _ports.ContainsKey(link.InputPortId);

    private static bool IsControlPort(Port port)
    {
        if (port.Direction is not ("in" or "out"))
            return false;

        if (port.FormatDsp?.Contains("midi",
                StringComparison.OrdinalIgnoreCase) == true)
            return true;

        var node = FrSonic.NodeRegistry.GetByObjectId(port.Node.Id);
        return node?.Media.Class == "Midi/Bridge";
    }

    private static string DisplayName(Port? port)
    {
        if (port is null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(port.Alias))
            return port.Alias;

        var node = FrSonic.NodeRegistry.GetByObjectId(port.Node.Id);
        var nodeName = node?.Description ?? node?.Name ?? $"Node {port.Node.Id}";
        var portName = port.Name ?? port.Channel ?? $"Port {port.ObjectId}";
        return $"{nodeName}: {portName}";
    }

    private static Port? PortById(ulong objectId) =>
        FrSonic.PortRegistry.GetByObjectId(objectId);

    private void OnPortChanged(Port _) => PostRefresh();
    private void OnLinkChanged(Link _) => PostRefresh();

    private void PostRefresh()
    {
        Dispatcher.UIThread.Post(Refresh);
    }

    public void Dispose()
    {
        FrSonic.PortRegistry.Added -= OnPortChanged;
        FrSonic.PortRegistry.Deleted -= OnPortChanged;
        FrSonic.LinkRegistry.Added -= OnLinkChanged;
        FrSonic.LinkRegistry.Deleted -= OnLinkChanged;
    }
}
