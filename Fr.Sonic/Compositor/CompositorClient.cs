using System.Text.Json;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Model.Params;

namespace Fr.Sonic.Compositor;

public sealed class CompositorClient : IDisposable
{
    private readonly Node _node;
    private readonly Lock _lock = new();
    private CompositorParams? _params;
    private Task<CompositorParams?>? _initialLoadTask;
    private bool _disposed;

    public CompositorClient(Node node)
    {
        _node = node;
        _node.ParamsChanged += OnParamsChanged;
    }

    public event Action<CompositorParams>? ParamsChanged;
    public event Action<int>? ActiveSceneIndexChanged;
    public event Action<IReadOnlyList<CompositorSceneInfo>>? ScenesChanged;

    public Task<CompositorParams?> GetParamsAsync()
    {
        lock (_lock)
        {
            if (_params is not null)
                return Task.FromResult<CompositorParams?>(_params);

            return _initialLoadTask ??= LoadInitialParamsAsync();
        }
    }

    public void SetActiveSceneIndex(int index) =>
        _node.SetParam("active_scene_index", index);

    /// <summary>
    /// Sends a partial per-object update, e.g.
    /// <c>SetObjectParams(0, new { dst_x = 100, visible = true })</c>.
    /// Every field besides the object index is optional on the compositor
    /// side - only include the fields that actually changed.
    /// </summary>
    public void SetObjectParams(int objectIndex, object fields)
    {
        using var document = JsonSerializer.SerializeToDocument(fields);
        var payload = new Dictionary<string, JsonElement> { ["object"] = JsonSerializer.SerializeToElement(objectIndex) };
        foreach (var property in document.RootElement.EnumerateObject())
            payload[property.Name] = property.Value;

        _node.SetParam("object_params", JsonSerializer.Serialize(payload));
    }

    private async Task<CompositorParams?> LoadInitialParamsAsync()
    {
        var parameters = await _node.Params.ConfigureAwait(false);
        var parsed = CompositorParamParser.Parse(parameters);
        if (parsed is null)
            return null;

        lock (_lock)
        {
            _params ??= parsed;
            return _params;
        }
    }

    private void OnParamsChanged(Dictionary<string, IParameter>? parameters)
    {
        var parsed = CompositorParamParser.Parse(parameters);
        if (parsed is null)
            return;

        CompositorParams? previous;
        lock (_lock)
        {
            previous = _params;
            _params = parsed;
            _initialLoadTask = Task.FromResult<CompositorParams?>(parsed);
        }

        ParamsChanged?.Invoke(parsed);

        if (previous?.ActiveSceneIndex != parsed.ActiveSceneIndex)
            ActiveSceneIndexChanged?.Invoke(parsed.ActiveSceneIndex);
        if (!ScenesEqual(previous?.Scenes, parsed.Scenes))
            ScenesChanged?.Invoke(parsed.Scenes);
    }

    private static bool ScenesEqual(
        IReadOnlyList<CompositorSceneInfo>? previous,
        IReadOnlyList<CompositorSceneInfo> current)
    {
        if (previous is null || previous.Count != current.Count)
            return false;

        for (var i = 0; i < current.Count; ++i)
            if (previous[i] != current[i])
                return false;

        return true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _node.ParamsChanged -= OnParamsChanged;
        _disposed = true;
    }
}
