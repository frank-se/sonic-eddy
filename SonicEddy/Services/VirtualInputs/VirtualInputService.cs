using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fr.Sonic;
using Fr.Sonic.Model.Objects;
using SonicEddy.Contracts.VirtualInputs;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Wireplumber;

namespace SonicEddy.Services.VirtualInputs;

public class VirtualInputService(
    IAppDataService appDataService,
    IWireplumberService wireplumberService)
    : IVirtualInputService, IDisposable
{
    private const string CaptureNodeMediaClass = "Stream/Input/Audio";
    private const string PlaybackNodeMediaClass = "Stream/Output/Audio";
    private const string NodeNamePrefix = "virtual-input-";
    private const string PlaybackNodeNameSuffix = "-playback";

    public static bool IsVirtualInputPlaybackNode(Node node) =>
        node.Name?.StartsWith(NodeNamePrefix) == true &&
        node.Name.EndsWith(PlaybackNodeNameSuffix);
    private static readonly List<string> StereoAudioPosition = ["FL", "FR"];
    private readonly List<VirtualInputConfig> _configuredInputs = [];
    private readonly Dictionary<Guid, VirtualInput> _activeInputs = [];
    private readonly HashSet<Guid> _inputsBeingCreated = [];
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private readonly SemaphoreSlim _restoreLock = new(1, 1);
    private readonly SemaphoreSlim _storeLock = new(1, 1);
    private readonly object _lock = new();
    private bool _initialized;

    public List<VirtualInput> VirtualInputs { get; } = [];

    public async Task InitializeAsync()
    {
        await _initializeLock.WaitAsync();
        try
        {
            if (_initialized) return;

            var config = await appDataService.LoadVirtualInputsConfig();
            lock (_lock)
                _configuredInputs.AddRange(config?.Inputs ?? []);

            wireplumberService.NodeAdded += OnNodeAdded;
            FrSonic.PortRegistry.Added += OnPortAdded;
            await ApplyConfiguredInputsAsync();
            _initialized = true;
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    public async Task AddVirtualInput(string name, Node node, Port[] ports)
    {
        await InitializeAsync();

        var id = Guid.NewGuid();
        await CreateVirtualInputAsync(id, name, node, ports);

        lock (_lock)
            _configuredInputs.Add(BuildConfig(id, name, node, ports));

        await StoreConfigAsync();
    }

    private async Task CreateVirtualInputAsync(Guid id, string name, Node node,
        Port[] ports)
    {
        var fullName = $"virtual-input-{name}";

        var captureNodeAudioPosition =
            ports.Select(p => p.Channel).OfType<string>().ToList();

        var loopback = await wireplumberService.CreateLoopbackModule(
            fullName, new()
            {
                CaptureProps = new()
                {
                    Linger = true,
                    Name = $"{fullName}-capture",
                    Description =
                        $"{fullName}-capture",
                    DontFallback = true,
                    MediaClass = CaptureNodeMediaClass,
                    TargetObject = node.ObjectSerial.ToString(),
                    AudioPosition = captureNodeAudioPosition,
                    ChannelMixUpmix = ports.Length == 1 ? true : null,
                    ChannelMixUpmixMethod = ports.Length == 1 ? "simple" : null
                },
                PlaybackProps = new()
                {
                    Linger = true,
                    Name = $"{fullName}-playback",
                    Description =
                        $"{fullName}-playback",
                    AudioPosition = ports.Length == 1 ? ["MONO"] : StereoAudioPosition,
                    MediaClass = PlaybackNodeMediaClass,
                    DontFallback = true,
                    AutoConnect = false,
                    ChannelMixUpmix = ports.Length == 1 ? true : null,
                    ChannelMixUpmixMethod = ports.Length == 1 ? "simple" : null
                }
            });

        var virtualInput = new VirtualInput(name, node, ports, loopback);
        
        VirtualInputs.Add(virtualInput);
        lock (_lock)
            _activeInputs[id] = virtualInput;

        Added?.Invoke(virtualInput);
    }

    public event Action<VirtualInput>? Added;

    public async Task DeleteVirtualInput(VirtualInput virtualInput)
    {
        lock (_lock)
        {
            var id = _activeInputs
                .FirstOrDefault(kv => ReferenceEquals(kv.Value, virtualInput)).Key;
            _activeInputs.Remove(id);
            _configuredInputs.RemoveAll(c => c.Id == id);
        }

        VirtualInputs.Remove(virtualInput);
        await StoreConfigAsync();
    }

    private async Task ApplyConfiguredInputsAsync()
    {
        await _restoreLock.WaitAsync();
        try
        {
            VirtualInputConfig[] pending;
            lock (_lock)
            {
                pending = _configuredInputs.Where(config =>
                        !_activeInputs.ContainsKey(config.Id) &&
                        _inputsBeingCreated.Add(config.Id))
                    .ToArray();
            }

            foreach (var config in pending)
            {
                try
                {
                    var node = FrSonic.NodeRegistry.Objects.FirstOrDefault(
                        candidate => string.Equals(candidate.Name,
                            config.NodeName, StringComparison.Ordinal));
                    if (node is null) continue;

                    var nodePorts = FrSonic.PortRegistry.Objects
                        .Where(port => port.Node.Id == node.ObjectId).ToArray();
                    var ports = config.Ports
                        .Select(port => FindPort(nodePorts, port))
                        .ToArray();
                    if (ports.Any(port => port is null)) continue;

                    await CreateVirtualInputAsync(config.Id, config.Name, node,
                        ports.Cast<Port>().ToArray());
                }
                catch
                {
                    // Retain the configuration and retry as the graph changes.
                }
                finally
                {
                    lock (_lock)
                        _inputsBeingCreated.Remove(config.Id);
                }
            }
        }
        finally
        {
            _restoreLock.Release();
        }
    }

    private async Task StoreConfigAsync()
    {
        await _storeLock.WaitAsync();
        try
        {
            VirtualInputConfig[] inputs;
            lock (_lock)
                inputs = _configuredInputs.ToArray();

            await appDataService.StoreVirtualInputsConfig(new()
            {
                Inputs = inputs.ToList()
            });
        }
        finally
        {
            _storeLock.Release();
        }
    }

    private static VirtualInputConfig BuildConfig(Guid id, string name,
        Node node, IEnumerable<Port> ports) => new()
    {
        Id = id,
        Name = name,
        NodeName = node.Name ?? string.Empty,
        Ports = ports.Select(port => new VirtualInputPortConfig
        {
            Name = port.Name ?? string.Empty,
            Alias = port.Alias ?? string.Empty,
            Channel = port.Channel ?? string.Empty
        }).ToList()
    };

    private static Port? FindPort(IEnumerable<Port> ports,
        VirtualInputPortConfig config)
    {
        var candidates = ports.ToArray();
        if (!string.IsNullOrEmpty(config.Alias))
        {
            var byAlias = candidates.FirstOrDefault(port =>
                string.Equals(port.Alias, config.Alias,
                    StringComparison.Ordinal));
            if (byAlias is not null) return byAlias;
        }

        var byName = candidates.FirstOrDefault(port =>
            string.Equals(port.Name, config.Name, StringComparison.Ordinal));
        if (byName is not null) return byName;

        return candidates.FirstOrDefault(port =>
            string.Equals(port.Channel, config.Channel,
                StringComparison.Ordinal));
    }

    private void OnNodeAdded(Node node) => _ = ApplyConfiguredInputsAsync();

    private void OnPortAdded(Port port) => _ = ApplyConfiguredInputsAsync();

    public void Dispose()
    {
        wireplumberService.NodeAdded -= OnNodeAdded;
        FrSonic.PortRegistry.Added -= OnPortAdded;
        _initializeLock.Dispose();
        _restoreLock.Dispose();
        _storeLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
