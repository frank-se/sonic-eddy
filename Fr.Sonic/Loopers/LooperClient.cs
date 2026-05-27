using Fr.Sonic.Model.Objects;
using Fr.Sonic.Model.Params;

namespace Fr.Sonic.Loopers;

public sealed class LooperClient : IDisposable
{
    private readonly Node _node;
    private readonly Lock _lock = new();
    private LooperParams? _params;
    private Task<LooperParams?>? _initialLoadTask;
    private bool _disposed;

    public LooperClient(Node node)
    {
        _node = node;
        _node.ParamsChanged += OnParamsChanged;
    }

    public event Action<LooperParams>? ParamsChanged;
    public event Action<float?>? MixChanged;
    public event Action<IReadOnlyList<LooperCommand>>? CommandsChanged;
    public event Action<LooperState?>? StateChanged;

    public Task<LooperParams?> GetParamsAsync()
    {
        lock (_lock)
        {
            if (_params is not null)
                return Task.FromResult<LooperParams?>(_params);

            return _initialLoadTask ??= LoadInitialParamsAsync();
        }
    }

    public async Task<float?> GetMixAsync() =>
        (await GetParamsAsync().ConfigureAwait(false))?.Mix;

    public async Task<IReadOnlyList<LooperCommand>> GetCommandsAsync() =>
        (await GetParamsAsync().ConfigureAwait(false))?.Commands ?? [];

    public async Task<LooperState?> GetStateAsync() =>
        (await GetParamsAsync().ConfigureAwait(false))?.State;

    private async Task<LooperParams?> LoadInitialParamsAsync()
    {
        var parameters = await _node.Params.ConfigureAwait(false);
        var parsed = LooperParamParser.Parse(parameters);
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
        var parsed = LooperParamParser.Parse(parameters);
        if (parsed is null)
            return;

        LooperParams? previous;
        lock (_lock)
        {
            previous = _params;
            _params = parsed;
            _initialLoadTask = Task.FromResult<LooperParams?>(parsed);
        }

        ParamsChanged?.Invoke(parsed);

        if (previous?.Mix != parsed.Mix)
            MixChanged?.Invoke(parsed.Mix);
        if (!CommandsEqual(previous?.Commands, parsed.Commands))
            CommandsChanged?.Invoke(parsed.Commands);
        if (previous?.State != parsed.State)
            StateChanged?.Invoke(parsed.State);
    }

    private static bool CommandsEqual(
        IReadOnlyList<LooperCommand>? previous,
        IReadOnlyList<LooperCommand> current)
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
