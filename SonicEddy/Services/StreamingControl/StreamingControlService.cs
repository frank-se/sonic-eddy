using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Fr.Sonic;
using Fr.Sonic.Compositor;
using Fr.Sonic.Model.Objects;

namespace SonicEddy.Services.StreamingControl;

public sealed class StreamingControlService : IStreamingControlService, IDisposable
{
    private readonly string _compositorOutputNodeName;

    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, Task<SceneFileConfig?>> _sceneFileCache = new();
    private readonly ConcurrentDictionary<(int, int), ObjectState> _objectStates = new();
    private CompositorClient? _client;
    private bool _initialized;

    // outputNodeName: which compositor instance's output node to discover -
    // see CompositorInstanceNames. Two instances of this service, each with
    // a different name, are what let the T-bar M/E switcher's two panels
    // (and the gamepad's two TargetStates) stay fully independent.
    public StreamingControlService(string outputNodeName)
    {
        _compositorOutputNodeName = outputNodeName;
        FrSonic.NodeRegistry.Added += OnNodeAdded;
        FrSonic.NodeRegistry.Deleted += OnNodeDeleted;
    }

    public CompositorClient? Client
    {
        get
        {
            lock (_lock)
                return _client;
        }
    }

    public event Action? ConnectionChanged;

    public Task InitializeAsync()
    {
        lock (_lock)
        {
            if (_initialized) return Task.CompletedTask;
            _initialized = true;
        }

        var existing = FrSonic.NodeRegistry.Objects
            .FirstOrDefault(node => node.Name == _compositorOutputNodeName);
        if (existing is not null)
            Connect(existing);

        return Task.CompletedTask;
    }

    public Task<SceneFileConfig?> LoadSceneFileAsync(string path) =>
        _sceneFileCache.GetOrAdd(path, SceneFile.LoadAsync);

    public event Action<int, int>? ObjectStateChanged;

    public ObjectState GetOrCreateObjectState(int sceneIndex, int objectIndex, SceneFileObject baseline) =>
        _objectStates.GetOrAdd((sceneIndex, objectIndex), _ => new ObjectState
        {
            X = baseline.X,
            Y = baseline.Y,
            FlipHorizontal = baseline.FlipHorizontal,
            FlipVertical = baseline.FlipVertical,
        });

    public void UpdateObjectState(int sceneIndex, int objectIndex, Action<ObjectState> mutate)
    {
        if (!_objectStates.TryGetValue((sceneIndex, objectIndex), out var state))
            return; // must have been read (GetOrCreateObjectState) before it can be mutated

        mutate(state);
        ObjectStateChanged?.Invoke(sceneIndex, objectIndex);
    }

    public (int SceneIndex, int FlatIndex)? CurrentSelection { get; private set; }
    public event Action? SelectionChanged;

    public void SelectObject(int sceneIndex, int flatIndex)
    {
        CurrentSelection = (sceneIndex, flatIndex);
        SelectionChanged?.Invoke();
    }

    private void OnNodeAdded(Node node)
    {
        if (node.Name != _compositorOutputNodeName) return;
        Connect(node);
    }

    private void OnNodeDeleted(Node node)
    {
        if (node.Name != _compositorOutputNodeName) return;

        bool changed;
        lock (_lock)
        {
            changed = _client is not null;
            _client?.Dispose();
            _client = null;
        }

        if (changed)
            ConnectionChanged?.Invoke();
    }

    private void Connect(Node node)
    {
        lock (_lock)
        {
            _client?.Dispose();
            _client = new CompositorClient(node);
        }

        ConnectionChanged?.Invoke();
    }

    public void Dispose()
    {
        FrSonic.NodeRegistry.Added -= OnNodeAdded;
        FrSonic.NodeRegistry.Deleted -= OnNodeDeleted;

        lock (_lock)
        {
            _client?.Dispose();
            _client = null;
        }
    }
}
