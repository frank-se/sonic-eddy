using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fr.Sonic;
using Fr.Sonic.Model.Objects;
using SonicEddy.Contracts.MidiSync;
using SonicEddy.Services.AppData;

namespace SonicEddy.Services.MidiSync;

public sealed class MidiSyncLinkService : IMidiSyncLinkService
{
    private const string MidiSyncNodeName = "se.midi_sync";
    private readonly IAppDataService _appDataService;
    private readonly HashSet<MidiSyncPortKey> _configuredPorts = [];
    private readonly object _lock = new();
    private readonly SemaphoreSlim _storeLock = new(1, 1);
    private bool _initialized;
    private CancellationTokenSource? _debounceCts;
    private readonly object _debounceLock = new();

    public event Action? Changed;

    public bool IsSyncOutputAvailable => FindSyncOutputPort() is not null;

    public MidiSyncLinkService(IAppDataService appDataService)
    {
        _appDataService = appDataService;
        FrSonic.NodeRegistry.Added += OnPipeWireObjectChanged;
        FrSonic.PortRegistry.Added += OnPipeWireObjectChanged;
        FrSonic.LinkRegistry.Added += OnPipeWireObjectChanged;
        FrSonic.LinkRegistry.Deleted += OnPipeWireObjectChanged;
    }

    public async Task InitializeAsync()
    {
        var config = await _appDataService.LoadMidiSyncConfig();
        lock (_lock)
        {
            if (_initialized)
                return;

            foreach (var port in config?.Ports ?? [])
                _configuredPorts.Add(new(port.NodeName, port.PortName));
            _initialized = true;
        }

        ApplyConfiguredLinks();
        Changed?.Invoke();
    }

    public IReadOnlyCollection<MidiSyncPortState> GetPorts()
    {
        var syncOutputPort = FindSyncOutputPort();
        var linkedInputPortIds = syncOutputPort is null
            ? []
            : FrSonic.LinkRegistry.Objects
                .Where(link => link.OutputPortId == syncOutputPort.ObjectId)
                .Select(link => link.InputPortId)
                .ToHashSet();

        return FrSonic.PortRegistry.Objects
            .Where(IsMidiInputPort)
            .OrderBy(DisplayName)
            .Select(port =>
            {
                var linked = linkedInputPortIds.Contains(port.ObjectId);
                var configured = IsConfigured(port);
                return new MidiSyncPortState(port, linked || configured,
                    linked);
            })
            .ToList();
    }

    public void SetReceivesSync(Port port, bool receivesSync)
    {
        var syncOutputPort = FindSyncOutputPort();
        if (syncOutputPort is not null)
        {
            if (receivesSync && !HasLink(syncOutputPort, port))
                FrSonic.LinkFactory.CreateLink(syncOutputPort, port);
            else if (!receivesSync)
                DeleteExistingLink(syncOutputPort, port);
        }

        var key = BuildPortKey(port);
        lock (_lock)
        {
            if (receivesSync)
                _configuredPorts.Add(key);
            else
                _configuredPorts.Remove(key);
        }

        _ = StoreConfigAsync();
        Changed?.Invoke();
    }

    private void ApplyConfiguredLinks()
    {
        var syncOutputPort = FindSyncOutputPort();
        if (syncOutputPort is null)
            return;

        foreach (var port in FrSonic.PortRegistry.Objects.Where(IsMidiInputPort))
        {
            if (!IsConfigured(port) || HasLink(syncOutputPort, port))
                continue;

            FrSonic.LinkFactory.CreateLink(syncOutputPort, port);
        }
    }

    private async Task StoreConfigAsync()
    {
        await _storeLock.WaitAsync();
        try
        {
            MidiSyncPortKey[] configuredPorts;
            lock (_lock)
                configuredPorts = _configuredPorts.ToArray();

            var config = new MidiSyncConfig
            {
                Ports = configuredPorts
                    .OrderBy(port => port.NodeName)
                    .ThenBy(port => port.PortName)
                    .Select(port => new MidiSyncPortConfig
                    {
                        NodeName = port.NodeName,
                        PortName = port.PortName
                    })
                    .ToList()
            };
            await _appDataService.StoreMidiSyncConfig(config);
        }
        finally
        {
            _storeLock.Release();
        }
    }

    private void OnPipeWireObjectChanged<T>(T _)
    {
        if (!_initialized) return;
        ScheduleApplyLinks();
    }

    private void ScheduleApplyLinks()
    {
        CancellationTokenSource cts;
        lock (_debounceLock)
        {
            _debounceCts?.Cancel();
            _debounceCts = cts = new CancellationTokenSource();
        }
        _ = Task.Delay(400, cts.Token).ContinueWith(t =>
        {
            if (!t.IsCompletedSuccessfully) return;
            ApplyConfiguredLinks();
            Changed?.Invoke();
        });
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

    private bool IsConfigured(Port port)
    {
        lock (_lock)
            return _configuredPorts.Contains(BuildPortKey(port));
    }

    private static bool HasLink(Port syncOutputPort, Port inputPort) =>
        FrSonic.LinkRegistry.Objects.Any(link =>
            link.OutputPortId == syncOutputPort.ObjectId &&
            link.InputPortId == inputPort.ObjectId);

    private static void DeleteExistingLink(Port syncOutputPort, Port inputPort)
    {
        var links = FrSonic.LinkRegistry.Objects.Where(link =>
            link.OutputPortId == syncOutputPort.ObjectId &&
            link.InputPortId == inputPort.ObjectId).ToList();
        foreach (var link in links)
            FrSonic.LinkFactory.DeleteLink(link);
    }

    private static MidiSyncPortKey BuildPortKey(Port port)
    {
        var node = FrSonic.NodeRegistry.GetByObjectId(port.Node.Id);
        return new(node?.Name ?? string.Empty,
            port.Name ?? port.Alias ?? port.Channel ?? string.Empty);
    }

    private readonly record struct MidiSyncPortKey(
        string NodeName,
        string PortName);
}
