using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fr.Sonic;
using Fr.Sonic.Model.Config.ClickSync;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;
using Fr.Sonic.Sync;
using SonicEddy.Contracts.ClickSync;
using SonicEddy.Services.AppData;
using ContractConverter = SonicEddy.Contracts.ClickSync.ClickSyncConverterConfig;
using NativeConverterConfig =
    Fr.Sonic.Model.Config.ClickSync.ClickSyncConverterConfig;

namespace SonicEddy.Services.ClickSync;

public sealed class ClickSyncService : IClickSyncService, IDisposable
{
    private readonly IAppDataService _appDataService;
    private readonly List<ContractConverter> _configuredConverters = [];
    private readonly Dictionary<Guid, ClickSyncConverter> _activeConverters =
        [];
    private readonly HashSet<Guid> _convertersBeingCreated = [];
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private readonly SemaphoreSlim _restoreLock = new(1, 1);
    private readonly SemaphoreSlim _storeLock = new(1, 1);
    private readonly object _lock = new();
    private SyncClient? _syncClient;
    private bool _initialized;

    public event Action? Changed;

    public bool CanCreate =>
        _syncClient?.TransportState() == SyncTransportState.Stopped;

    public ClickSyncService(IAppDataService appDataService)
    {
        _appDataService = appDataService;
        FrSonic.NodeRegistry.Added += OnNodeAdded;
        FrSonic.PortRegistry.Added += OnPortAdded;
        FrSonic.LinkRegistry.Added += OnLinkChanged;
        FrSonic.LinkRegistry.Deleted += OnLinkChanged;
        EnsureSyncClient();
    }

    public async Task InitializeAsync()
    {
        await _initializeLock.WaitAsync();
        try
        {
            if (_initialized) return;

            var config = await _appDataService.LoadClickSyncConfig();
            lock (_lock)
                _configuredConverters.AddRange(config?.Converters ?? []);

            _initialized = true;
            await ApplyConfiguredConvertersAsync();
            ApplyConfiguredLinks();
            Changed?.Invoke();
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    public IReadOnlyCollection<ClickSyncConverterState> GetConverters()
    {
        lock (_lock)
            return _configuredConverters
                .OrderBy(converter => converter.Name)
                .Select(converter => new ClickSyncConverterState(
                    converter.Id, converter.Name,
                    converter.PulsesPerQuarterNote, converter.PulseLengthMs,
                    converter.PulseAmplitude))
                .ToList();
    }

    public IReadOnlyCollection<ClickSyncPortState> GetPorts(
        Guid converterId, ClickSyncOutput output)
    {
        ClickSyncTargetPortConfig[] targets;
        ClickSyncConverter? module;
        lock (_lock)
        {
            var config =
                _configuredConverters.FirstOrDefault(x => x.Id == converterId);
            if (config is null) return [];
            targets = TargetList(config, output).ToArray();
            _activeConverters.TryGetValue(converterId, out module);
        }

        var source = module is null ? null : FindSourcePort(module, output);
        var linkedInputIds = source is null
            ? []
            : FrSonic.LinkRegistry.Objects
                .Where(link => link.OutputPortId == source.ObjectId)
                .Select(link => link.InputPortId)
                .ToHashSet();

        return FrSonic.PortRegistry.Objects
            .Where(IsAudioInputPort)
            .OrderBy(DisplayName)
            .Select(port => new ClickSyncPortState(
                port,
                targets.Any(target => Matches(port, target)),
                linkedInputIds.Contains(port.ObjectId)))
            .ToList();
    }

    public async Task AddConverterAsync(string name,
        uint pulsesPerQuarterNote, double pulseLengthMs, float pulseAmplitude)
    {
        await InitializeAsync();
        if (!CanCreate)
            throw new InvalidOperationException(
                "Click sync converters can only be created while stopped.");
        if (pulsesPerQuarterNote is < 1 or > 96)
            throw new ArgumentOutOfRangeException(nameof(pulsesPerQuarterNote));
        if (pulseLengthMs is < 0.1 or > 20.0)
            throw new ArgumentOutOfRangeException(nameof(pulseLengthMs));

        var config = new ContractConverter
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(name) ? "Click sync" : name.Trim(),
            PulsesPerQuarterNote = pulsesPerQuarterNote,
            PulseLengthMs = pulseLengthMs,
            PulseAmplitude = Math.Clamp(pulseAmplitude, 0.0f, 1.0f)
        };

        await CreateConverterAsync(config);
        lock (_lock)
            _configuredConverters.Add(config);
        await StoreConfigAsync();
        Changed?.Invoke();
    }

    public async Task DeleteConverterAsync(Guid converterId)
    {
        ClickSyncConverter? module;
        lock (_lock)
        {
            _activeConverters.Remove(converterId, out module);
            _configuredConverters.RemoveAll(x => x.Id == converterId);
        }

        module?.Destroy();
        await StoreConfigAsync();
        Changed?.Invoke();
    }

    public void SetTarget(Guid converterId, ClickSyncOutput output, Port port,
        bool selected)
    {
        ClickSyncConverter? module;
        lock (_lock)
        {
            var converter =
                _configuredConverters.FirstOrDefault(x => x.Id == converterId);
            if (converter is null) return;
            _activeConverters.TryGetValue(converterId, out module);
            var targets = TargetList(converter, output);
            targets.RemoveAll(target => Matches(port, target));
            if (selected)
                targets.Add(BuildTarget(port));
        }

        var source = module is null ? null : FindSourcePort(module, output);
        if (!selected && source is not null)
        {
            foreach (var link in FrSonic.LinkRegistry.Objects.Where(link =>
                         link.OutputPortId == source.ObjectId &&
                         link.InputPortId == port.ObjectId).ToList())
                FrSonic.LinkFactory.DeleteLink(link);
        }
        ApplyConfiguredLinks();
        _ = StoreConfigAsync();
        Changed?.Invoke();
    }

    private async Task ApplyConfiguredConvertersAsync()
    {
        await _restoreLock.WaitAsync();
        try
        {
            ContractConverter[] pending;
            lock (_lock)
            {
                pending = _configuredConverters.Where(config =>
                        !_activeConverters.ContainsKey(config.Id) &&
                        _convertersBeingCreated.Add(config.Id))
                    .ToArray();
            }

            foreach (var config in pending)
            {
                try
                {
                    await CreateConverterAsync(config);
                }
                catch
                {
                    // Retain the configuration and retry after graph changes.
                }
                finally
                {
                    lock (_lock)
                        _convertersBeingCreated.Remove(config.Id);
                }
            }
        }
        finally
        {
            _restoreLock.Release();
        }
    }

    private async Task CreateConverterAsync(ContractConverter config)
    {
        var module = await FrSonic.ClickSyncConverterFactory
            .CreateClickSyncConverterAsync(new NativeConverterConfig(
                config.Id, config.Name, config.PulsesPerQuarterNote,
                config.PulseLengthMs, config.PulseAmplitude));
        lock (_lock)
            _activeConverters[config.Id] = module;
    }

    private void ApplyConfiguredLinks()
    {
        (ContractConverter Config, ClickSyncConverter Module)[] converters;
        lock (_lock)
            converters = _configuredConverters
                .Where(config => _activeConverters.ContainsKey(config.Id))
                .Select(config => (config, _activeConverters[config.Id]))
                .ToArray();

        foreach (var (config, module) in converters)
        {
            ApplyLinks(FindSourcePort(module, ClickSyncOutput.Click),
                config.ClickTargets);
            ApplyLinks(FindSourcePort(module, ClickSyncOutput.Reset),
                config.ResetTargets);
            ApplyLinks(FindSourcePort(module, ClickSyncOutput.Run),
                config.RunTargets);
        }
    }

    private static void ApplyLinks(Port? source,
        IEnumerable<ClickSyncTargetPortConfig> targets)
    {
        if (source is null) return;
        foreach (var target in targets)
        {
            var port = FrSonic.PortRegistry.Objects.FirstOrDefault(candidate =>
                IsAudioInputPort(candidate) && Matches(candidate, target));
            if (port is null || HasLink(source, port)) continue;
            FrSonic.LinkFactory.CreateLink(source, port);
        }
    }

    private async Task StoreConfigAsync()
    {
        await _storeLock.WaitAsync();
        try
        {
            ContractConverter[] converters;
            lock (_lock)
                converters = _configuredConverters.ToArray();
            await _appDataService.StoreClickSyncConfig(new()
            {
                Converters = converters.ToList()
            });
        }
        finally
        {
            _storeLock.Release();
        }
    }

    private void EnsureSyncClient()
    {
        if (_syncClient is not null) return;
        var syncMaster = FrSonic.NodeRegistry.Objects
            .FirstOrDefault(node => node.Name == "se.sync_master");
        if (syncMaster is null) return;
        _syncClient = new SyncClient(syncMaster);
        _syncClient.SnapshotChanged += OnSyncSnapshotChanged;
    }

    private void OnSyncSnapshotChanged(SyncSnapshot _) => Changed?.Invoke();

    private void OnNodeAdded(Node node)
    {
        _ = node;
        EnsureSyncClient();
        if (_initialized)
            _ = ApplyConfiguredConvertersAsync().ContinueWith(_ =>
            {
                ApplyConfiguredLinks();
                Changed?.Invoke();
            });
    }

    private void OnPortAdded(Port _)
    {
        if (!_initialized) return;
        ApplyConfiguredLinks();
        Changed?.Invoke();
    }

    private void OnLinkChanged<T>(T _)
    {
        if (!_initialized) return;
        ApplyConfiguredLinks();
        Changed?.Invoke();
    }

    private static Port? FindSourcePort(ClickSyncConverter converter,
        ClickSyncOutput output)
    {
        var node = output switch
        {
            ClickSyncOutput.Click => converter.ClickNode,
            ClickSyncOutput.Reset => converter.ResetNode,
            _ => converter.RunNode
        };
        return FrSonic.PortRegistry.Objects.FirstOrDefault(port =>
            port.Node.Id == node.ObjectId && port.Direction == "out");
    }

    private static bool IsAudioInputPort(Port port)
    {
        if (port.Direction != "in") return false;
        var node = FrSonic.NodeRegistry.GetByObjectId(port.Node.Id);
        return node is { Media.Class: "Audio/Sink" or "Stream/Input/Audio" } &&
               !(node.Name?.StartsWith("se.click_sync.",
                   StringComparison.Ordinal) ?? false);
    }

    private static string DisplayName(Port port) =>
        port.Alias ?? port.Name ?? $"Port {port.ObjectId}";

    private static bool HasLink(Port source, Port target) =>
        FrSonic.LinkRegistry.Objects.Any(link =>
            link.OutputPortId == source.ObjectId &&
            link.InputPortId == target.ObjectId);

    private static ClickSyncTargetPortConfig BuildTarget(Port port)
    {
        var node = FrSonic.NodeRegistry.GetByObjectId(port.Node.Id);
        return new()
        {
            NodeName = node?.Name ?? string.Empty,
            PortName = port.Name ?? string.Empty,
            PortAlias = port.Alias ?? string.Empty
        };
    }

    private static bool Matches(Port port, ClickSyncTargetPortConfig target)
    {
        var node = FrSonic.NodeRegistry.GetByObjectId(port.Node.Id);
        if (!string.Equals(node?.Name, target.NodeName,
                StringComparison.Ordinal))
            return false;
        if (!string.IsNullOrEmpty(target.PortName) &&
            string.Equals(port.Name, target.PortName,
                StringComparison.Ordinal))
            return true;
        return !string.IsNullOrEmpty(target.PortAlias) &&
               string.Equals(port.Alias, target.PortAlias,
                   StringComparison.Ordinal);
    }

    private static List<ClickSyncTargetPortConfig> TargetList(
        ContractConverter converter, ClickSyncOutput output) =>
        output switch
        {
            ClickSyncOutput.Click => converter.ClickTargets,
            ClickSyncOutput.Reset => converter.ResetTargets,
            _ => converter.RunTargets
        };

    public void Dispose()
    {
        FrSonic.NodeRegistry.Added -= OnNodeAdded;
        FrSonic.PortRegistry.Added -= OnPortAdded;
        FrSonic.LinkRegistry.Added -= OnLinkChanged;
        FrSonic.LinkRegistry.Deleted -= OnLinkChanged;
        if (_syncClient is not null)
        {
            _syncClient.SnapshotChanged -= OnSyncSnapshotChanged;
            _syncClient.Dispose();
        }
        _initializeLock.Dispose();
        _restoreLock.Dispose();
        _storeLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
