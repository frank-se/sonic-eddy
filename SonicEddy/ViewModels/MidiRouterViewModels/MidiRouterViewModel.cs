using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Threading;
using Fr.Sonic;
using Fr.Sonic.Model.Objects;
using ReactiveUI;

namespace SonicEddy.ViewModels.MidiRouterViewModels;

public sealed class MidiRouterViewModel : ViewModelBase, IDisposable
{
    public MidiRouterViewModel()
    {
        ConnectCommand = ReactiveCommand.Create(Connect);
        RefreshCommand = ReactiveCommand.Create(Refresh);

        FrSonic.PortRegistry.Added += OnPortChanged;
        FrSonic.PortRegistry.Deleted += OnPortChanged;
        FrSonic.LinkRegistry.Added += OnLinkChanged;
        FrSonic.LinkRegistry.Deleted += OnLinkChanged;

        Refresh();
    }

    public ObservableCollection<MidiRouterPortOptionViewModel> Sources { get; } = [];
    public ObservableCollection<MidiRouterPortOptionViewModel> Targets { get; } = [];
    public ObservableCollection<MidiRouteLinkViewModel> RouteLinks { get; } = [];

    public ICommand ConnectCommand { get; }
    public ICommand RefreshCommand { get; }

    public MidiRouterPortOptionViewModel? SelectedSource
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(CanConnect));
        }
    }

    public MidiRouterPortOptionViewModel? SelectedTarget
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(CanConnect));
        }
    }

    public bool CanConnect =>
        SelectedSource is not null &&
        SelectedTarget is not null &&
        !HasLink(SelectedSource.Port.ObjectId, SelectedTarget.Port.ObjectId);

    public string Status
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public void Refresh()
    {
        var selectedSourceId = SelectedSource?.Port.ObjectId;
        var selectedTargetId = SelectedTarget?.Port.ObjectId;

        Sources.Clear();
        Targets.Clear();
        RouteLinks.Clear();

        var controlPorts = FrSonic.PortRegistry.Objects
            .Where(IsControlPort)
            .OrderBy(DisplayName)
            .ToList();

        foreach (var source in controlPorts
                     .Where(port => port.Direction == "out")
                     .Select(port => new MidiRouterPortOptionViewModel(
                         port, DisplayName(port))))
            Sources.Add(source);

        foreach (var target in controlPorts
                     .Where(port => port.Direction == "in")
                     .Select(port => new MidiRouterPortOptionViewModel(
                         port, DisplayName(port))))
            Targets.Add(target);

        SelectedSource = Sources.FirstOrDefault(source =>
                             source.Port.ObjectId == selectedSourceId) ??
                         Sources.FirstOrDefault();
        SelectedTarget = Targets.FirstOrDefault(target =>
                             target.Port.ObjectId == selectedTargetId) ??
                         Targets.FirstOrDefault();

        foreach (var link in FrSonic.LinkRegistry.Objects
                     .Where(IsVisibleRoute)
                     .OrderBy(link => DisplayName(PortById(link.OutputPortId)))
                     .ThenBy(link => DisplayName(PortById(link.InputPortId))))
            AddRoute(link);

        Status =
            $"{Sources.Count} source ports, {Targets.Count} target ports, {RouteLinks.Count} routes.";
        this.RaisePropertyChanged(nameof(CanConnect));
    }

    private void Connect()
    {
        if (SelectedSource is null || SelectedTarget is null)
            return;

        var source = SelectedSource.Port;
        var target = SelectedTarget.Port;
        if (source.Direction != "out" || target.Direction != "in")
            return;
        if (HasLink(source.ObjectId, target.ObjectId))
            return;

        FrSonic.LinkFactory.CreateLink(source, target);
    }

    private static void DeleteRoute(ulong sourcePortId, ulong targetPortId)
    {
        foreach (var link in FrSonic.LinkRegistry.Objects
                     .Where(link => link.OutputPortId == sourcePortId &&
                                    link.InputPortId == targetPortId)
                     .ToList())
            FrSonic.LinkFactory.DeleteLink(link);
    }

    private void AddRoute(Link link)
    {
        var source = PortById(link.OutputPortId);
        var target = PortById(link.InputPortId);
        if (source is null || target is null)
            return;

        RouteLinks.Add(new(link.OutputPortId, link.InputPortId,
            DisplayName(source), DisplayName(target), DeleteRoute));
    }

    private static bool HasLink(ulong sourcePortId, ulong targetPortId) =>
        FrSonic.LinkRegistry.Objects.Any(link =>
            link.OutputPortId == sourcePortId &&
            link.InputPortId == targetPortId);

    private static bool IsVisibleRoute(Link link) =>
        IsControlPortId(link.OutputPortId) &&
        IsControlPortId(link.InputPortId);

    private static bool IsControlPortId(ulong objectId) =>
        PortById(objectId) is { } port && IsControlPort(port);

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
