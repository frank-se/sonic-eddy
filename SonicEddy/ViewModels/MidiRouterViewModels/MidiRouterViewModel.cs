using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Fr.Sonic;
using Fr.Sonic.Model.Objects;
using ReactiveUI;
using SonicEddy.Services.MidiRouter;
using Splat;

namespace SonicEddy.ViewModels.MidiRouterViewModels;

public sealed class MidiRouterViewModel : ViewModelBase, IDisposable
{
    private readonly IMidiRouterService _routerService;

    public MidiRouterViewModel()
    {
        _routerService = Locator.Current.GetService<IMidiRouterService>() ??
                         new MidiRouterService();
        _routerService.RoutesChanged += OnRoutesChanged;
        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync);
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
        !HasRoute(SelectedSource.Port.ObjectId, SelectedTarget.Port.ObjectId);

    public string Status
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string DropChannels
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string ChannelMap
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
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

        foreach (var route in _routerService.Routes
                     .OrderBy(route => DisplayName(route.Source))
                     .ThenBy(route => DisplayName(route.Target)))
            AddRoute(route);

        Status =
            $"{Sources.Count} source ports, {Targets.Count} target ports, {RouteLinks.Count} routes.";
        this.RaisePropertyChanged(nameof(CanConnect));
    }

    private async Task ConnectAsync()
    {
        if (SelectedSource is null || SelectedTarget is null)
            return;

        var source = SelectedSource.Port;
        var target = SelectedTarget.Port;
        if (source.Direction != "out" || target.Direction != "in")
            return;
        if (HasRoute(source.ObjectId, target.ObjectId))
            return;

        await _routerService.CreateRouteAsync(source, target,
            ParseManipulation());
    }

    private void DeleteRoute(Guid routeId)
    {
        _routerService.DeleteRoute(routeId);
    }

    private void AddRoute(MidiRoute route) =>
        RouteLinks.Add(new(route.Id, DisplayName(route.Source),
            DisplayName(route.Target), FormatManipulation(route.Manipulation),
            DeleteRoute));

    private bool HasRoute(ulong sourcePortId, ulong targetPortId) =>
        _routerService.Routes.Any(route =>
            route.Source.ObjectId == sourcePortId &&
            route.Target.ObjectId == targetPortId);

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

    private void OnPortChanged(Port _) => PostRefresh();
    private void OnLinkChanged(Link _) => PostRefresh();
    private void OnRoutesChanged() => PostRefresh();

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
        _routerService.RoutesChanged -= OnRoutesChanged;
    }

    private MidiManipulationConfig ParseManipulation()
    {
        var drops = DropChannels
            .Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseChannel)
            .OfType<int>()
            .Distinct()
            .Order()
            .ToList();

        var maps = ChannelMap
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseMap)
            .OfType<ChannelMapEntry>()
            .ToList();

        return new(drops, maps);
    }

    private static int? ParseChannel(string value) =>
        int.TryParse(value.Trim(), out var channel) && channel is >= 1 and <= 16
            ? channel
            : null;

    private static ChannelMapEntry? ParseMap(string value)
    {
        var parts = value.Split(['>', ':', '-'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return null;

        var from = ParseChannel(parts[0]);
        var to = ParseChannel(parts[1]);
        return from is null || to is null ? null : new(from.Value, to.Value);
    }

    private static string FormatManipulation(MidiManipulationConfig config)
    {
        if (config.IsPassthrough)
            return "passthrough";

        var parts = new List<string>();
        if (config.DropChannels.Count > 0)
            parts.Add($"drop {string.Join(",", config.DropChannels)}");
        if (config.ChannelMap.Count > 0)
            parts.Add("map " + string.Join(",",
                config.ChannelMap.Select(map => $"{map.From}>{map.To}")));
        return string.Join("; ", parts);
    }
}
