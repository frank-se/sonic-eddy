using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fr.Sonic;
using Fr.Sonic.Model.Config;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;
using SonicEddy.Contracts.ExternalEffects;
using SonicEddy.Services.AppData;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.Wireplumber;

namespace SonicEddy.Services.ExternalEffects;

public sealed class ExternalEffectService(
    IAppDataService appDataService,
    IWireplumberService wireplumberService) : IExternalEffectService, IDisposable
{
    private static readonly List<string> StereoPosition = ["FL", "FR"];
    private readonly List<ExternalEffectConfig> _effects = [];
    private readonly Dictionary<Guid, string> _usage = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _initialized;

    public IReadOnlyList<ExternalEffectConfig> Effects => _effects;
    public event Action? Changed;

    public async Task InitializeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_initialized) return;
            var config = await appDataService.LoadExternalEffectsConfig();
            _effects.AddRange(config?.Effects ?? []);
            wireplumberService.NodeAdded += OnGraphChanged;
            wireplumberService.NodeDeleted += OnGraphChanged;
            FrSonic.PortRegistry.Added += OnGraphChanged;
            FrSonic.PortRegistry.Deleted += OnGraphChanged;
            _initialized = true;
        }
        finally
        {
            _lock.Release();
        }

        Changed?.Invoke();
    }

    public async Task<ExternalEffectConfig> AddAsync(string name,
        Node inputNode, IReadOnlyList<Port> inputPorts, Node outputNode,
        IReadOnlyList<Port> outputPorts)
    {
        Validate(name, inputPorts, outputPorts);
        var config = Build(Guid.NewGuid(), name, inputNode, inputPorts,
            outputNode, outputPorts);
        await _lock.WaitAsync();
        try
        {
            _effects.Add(config);
            await StoreAsync();
        }
        finally
        {
            _lock.Release();
        }
        Changed?.Invoke();
        return config;
    }

    public async Task UpdateAsync(Guid id, string name, Node inputNode,
        IReadOnlyList<Port> inputPorts, Node outputNode,
        IReadOnlyList<Port> outputPorts)
    {
        Validate(name, inputPorts, outputPorts);
        await _lock.WaitAsync();
        try
        {
            if (_usage.ContainsKey(id))
                throw new InvalidOperationException(
                    "An external effect in use cannot be edited.");
            var index = _effects.FindIndex(effect => effect.Id == id);
            if (index < 0)
                throw new KeyNotFoundException($"External effect {id} not found.");
            _effects[index] = Build(id, name, inputNode, inputPorts, outputNode,
                outputPorts);
            await StoreAsync();
        }
        finally
        {
            _lock.Release();
        }
        Changed?.Invoke();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            if (_usage.ContainsKey(id))
                throw new InvalidOperationException(
                    "An external effect in use cannot be deleted.");
            _effects.RemoveAll(effect => effect.Id == id);
            await StoreAsync();
        }
        finally
        {
            _lock.Release();
        }
        Changed?.Invoke();
    }

    public bool IsAvailable(Guid id)
    {
        var effect = _effects.FirstOrDefault(candidate => candidate.Id == id);
        return effect is not null &&
               ResolveEndpoint(effect.InputNodeName, effect.InputPorts,
                   direction: "in") &&
               ResolveEndpoint(effect.OutputNodeName, effect.OutputPorts,
                   direction: "out");
    }

    public string? GetUsedBy(Guid id) =>
        _usage.GetValueOrDefault(id);

    public async Task<ExternalEffectInsertProcessor> CreateInsertAsync(Guid id,
        Node insertionInput, Node insertionOutput, string usedBy)
    {
        await InitializeAsync();
        ExternalEffectConfig effect;
        await _lock.WaitAsync();
        try
        {
            effect = _effects.FirstOrDefault(candidate => candidate.Id == id)
                     ?? throw new KeyNotFoundException(
                         $"External effect {id} not found.");
            if (_usage.TryGetValue(id, out var current))
                throw new InvalidOperationException(
                    $"External effect '{effect.Name}' is already used by {current}.");
            _usage.Add(id, usedBy);
        }
        finally
        {
            _lock.Release();
        }

        try
        {
            var prefix = $"external-effect-{id:N}-{Sanitize(usedBy)}";
            var inputLoopback = await wireplumberService.CreateLoopbackModule(
                $"{prefix}-input", new()
                {
                    CaptureProps = InsertCapture(
                        $"{prefix}-input-capture",
                        insertionInput.ObjectSerial.ToString(), StereoPosition),
                    PlaybackProps = InsertPlayback(
                        $"{prefix}-input-playback", effect.InputNodeName,
                        effect.InputPorts.Select(PortPosition).ToList())
                });
            LoopbackModule outputLoopback;
            try
            {
                outputLoopback = await wireplumberService.CreateLoopbackModule(
                    $"{prefix}-output", new()
                    {
                        CaptureProps = InsertCapture(
                            $"{prefix}-output-capture", effect.OutputNodeName,
                            effect.OutputPorts.Select(PortPosition).ToList()),
                        PlaybackProps = InsertPlayback(
                            $"{prefix}-output-playback",
                            insertionOutput.ObjectSerial.ToString(),
                            StereoPosition)
                    });
            }
            catch
            {
                inputLoopback.Destroy();
                throw;
            }

            Changed?.Invoke();
            return new(id, inputLoopback, outputLoopback, () => Release(id));
        }
        catch
        {
            Release(id);
            throw;
        }
    }

    private void Release(Guid id)
    {
        _usage.Remove(id);
        Changed?.Invoke();
    }

    private static NodePropertiesConfig InsertCapture(string name,
        string target, List<string> positions) => new()
    {
        Name = name,
        Description = name,
        Linger = true,
        AutoConnect = true,
        DontFallback = true,
        Passive = false,
        TargetObject = target,
        MediaClass = "Stream/Input/Audio",
        AudioPosition = positions
    };

    private static NodePropertiesConfig InsertPlayback(string name,
        string target, List<string> positions) => new()
    {
        Name = name,
        Description = name,
        Linger = true,
        AutoConnect = true,
        DontFallback = true,
        Passive = false,
        TargetObject = target,
        MediaClass = "Stream/Output/Audio",
        AudioPosition = positions
    };

    private bool ResolveEndpoint(string nodeName,
        IReadOnlyList<ExternalEffectPortConfig> ports, string direction)
    {
        var node = FrSonic.NodeRegistry.Objects.FirstOrDefault(candidate =>
            candidate.Name == nodeName && string.IsNullOrEmpty(candidate.Pmx.Tag));
        if (node is null) return false;
        var available = FrSonic.PortRegistry.Objects.Where(port =>
            port.Node.Id == node.ObjectId && port.Direction == direction).ToArray();
        return ports.Count == 2 &&
               ports.All(config => FindPort(available, config) is not null);
    }

    private static Port? FindPort(IEnumerable<Port> ports,
        ExternalEffectPortConfig config) =>
        ports.FirstOrDefault(port =>
            (!string.IsNullOrEmpty(config.Name) && port.Name == config.Name) ||
            (!string.IsNullOrEmpty(config.Alias) && port.Alias == config.Alias) ||
            (!string.IsNullOrEmpty(config.Channel) &&
             port.Channel == config.Channel));

    private async Task StoreAsync() =>
        await appDataService.StoreExternalEffectsConfig(new()
        {
            Effects = _effects.ToList()
        });

    private static ExternalEffectConfig Build(Guid id, string name,
        Node inputNode, IEnumerable<Port> inputPorts, Node outputNode,
        IEnumerable<Port> outputPorts) => new()
    {
        Id = id,
        Name = name,
        InputNodeName = inputNode.Name ?? string.Empty,
        InputPorts = inputPorts.Select(BuildPort).ToList(),
        OutputNodeName = outputNode.Name ?? string.Empty,
        OutputPorts = outputPorts.Select(BuildPort).ToList()
    };

    private static ExternalEffectPortConfig BuildPort(Port port) => new()
    {
        Name = port.Name ?? string.Empty,
        Alias = port.Alias ?? string.Empty,
        Channel = port.Channel ?? string.Empty
    };

    private static string PortPosition(ExternalEffectPortConfig port) =>
        !string.IsNullOrEmpty(port.Channel) ? port.Channel :
        !string.IsNullOrEmpty(port.Name) ? port.Name : port.Alias;

    private static void Validate(string name, IReadOnlyCollection<Port> inputs,
        IReadOnlyCollection<Port> outputs)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("An external effect requires a name.");
        if (inputs.Count != 2 || outputs.Count != 2)
            throw new ArgumentException(
                "An external effect requires two input and two output ports.");
        if (inputs.DistinctBy(port => port.ObjectId).Count() != 2 ||
            outputs.DistinctBy(port => port.ObjectId).Count() != 2)
            throw new ArgumentException("Left and right ports must be distinct.");
        if (inputs.Concat(outputs).Any(port =>
                string.IsNullOrEmpty(port.Channel)))
            throw new ArgumentException(
                "External effect ports require PipeWire channel positions.");
        if (inputs.Select(port => port.Channel).Distinct().Count() != 2 ||
            outputs.Select(port => port.Channel).Distinct().Count() != 2)
            throw new ArgumentException(
                "Left and right ports require distinct channel positions.");
    }

    private static string Sanitize(string value) =>
        new(value.Select(character =>
            char.IsLetterOrDigit(character) ? character : '-').ToArray());

    private void OnGraphChanged<T>(T _) => Changed?.Invoke();

    public void Dispose()
    {
        wireplumberService.NodeAdded -= OnGraphChanged;
        wireplumberService.NodeDeleted -= OnGraphChanged;
        FrSonic.PortRegistry.Added -= OnGraphChanged;
        FrSonic.PortRegistry.Deleted -= OnGraphChanged;
        GC.SuppressFinalize(this);
    }
}
