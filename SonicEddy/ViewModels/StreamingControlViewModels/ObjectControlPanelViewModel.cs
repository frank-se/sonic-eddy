using System;
using System.Windows.Input;
using Avalonia.Threading;
using Fr.Sonic.Compositor;
using ReactiveUI;
using SonicEddy.Services.StreamingControl;

namespace SonicEddy.ViewModels.StreamingControlViewModels;

// Reads/writes through IStreamingControlService's shared ObjectState cache
// (not private fields) - the compositor never echoes object_params back,
// so that cache, not this view model, is the source of truth for an
// object's "current" values. This matters once the gamepad dispatcher can
// also change the same object: both writers go through the same cache and
// notify each other via ObjectStateChanged, so this window always shows
// whatever's actually current, not a stale scene-file baseline.
public sealed class ObjectControlPanelViewModel : ViewModelBase, IDisposable
{
    private const int NudgeStep = 10;

    private readonly IStreamingControlService _service;
    private readonly CompositorClient _client;
    private readonly int _sceneIndex;
    private readonly int _objectIndex;
    private readonly ObjectState _state;

    public ObjectControlPanelViewModel(IStreamingControlService service, CompositorClient client,
        int sceneIndex, int objectIndex, SceneFileObject baseline, int canvasWidth, int canvasHeight)
    {
        _service = service;
        _client = client;
        _sceneIndex = sceneIndex;
        _objectIndex = objectIndex;
        MaxX = Math.Max(0, canvasWidth - baseline.Width);
        MaxY = Math.Max(0, canvasHeight - baseline.Height);

        _state = service.GetOrCreateObjectState(sceneIndex, objectIndex, baseline);
        _service.ObjectStateChanged += OnObjectStateChanged;

        MoveUpCommand = ReactiveCommand.Create(() => Nudge(0, -NudgeStep));
        MoveDownCommand = ReactiveCommand.Create(() => Nudge(0, NudgeStep));
        MoveLeftCommand = ReactiveCommand.Create(() => Nudge(-NudgeStep, 0));
        MoveRightCommand = ReactiveCommand.Create(() => Nudge(NudgeStep, 0));
        UnifyCommand = ReactiveCommand.Create(Unify);
    }

    public int MaxX { get; }
    public int MaxY { get; }

    // Rotate/Z-order have no backend support yet - the buttons stay
    // visible in the layout (per the sketch) but permanently disabled.
    public bool RotateEnabled => false;
    public bool ZOrderEnabled => false;

    public int X
    {
        get => _state.X;
        set
        {
            var clamped = Math.Clamp(value, 0, MaxX);
            if (clamped == _state.X) return;
            _service.UpdateObjectState(_sceneIndex, _objectIndex, s => s.X = clamped);
            _client.SetObjectParams(_objectIndex, new { dst_x = clamped });
        }
    }

    public int Y
    {
        get => _state.Y;
        set
        {
            var clamped = Math.Clamp(value, 0, MaxY);
            if (clamped == _state.Y) return;
            _service.UpdateObjectState(_sceneIndex, _objectIndex, s => s.Y = clamped);
            _client.SetObjectParams(_objectIndex, new { dst_y = clamped });
        }
    }

    public bool Visible
    {
        get => _state.Visible;
        set
        {
            if (value == _state.Visible) return;
            _service.UpdateObjectState(_sceneIndex, _objectIndex, s => s.Visible = value);
            _client.SetObjectParams(_objectIndex, new { visible = value });
        }
    }

    public bool FlipHorizontal
    {
        get => _state.FlipHorizontal;
        set
        {
            if (value == _state.FlipHorizontal) return;
            _service.UpdateObjectState(_sceneIndex, _objectIndex, s => s.FlipHorizontal = value);
            _client.SetObjectParams(_objectIndex, new { flip_horizontal = value });
        }
    }

    public bool FlipVertical
    {
        get => _state.FlipVertical;
        set
        {
            if (value == _state.FlipVertical) return;
            _service.UpdateObjectState(_sceneIndex, _objectIndex, s => s.FlipVertical = value);
            _client.SetObjectParams(_objectIndex, new { flip_vertical = value });
        }
    }

    public float RedGain
    {
        get => _state.RedGain;
        set
        {
            if (value.Equals(_state.RedGain)) return;
            _service.UpdateObjectState(_sceneIndex, _objectIndex, s => s.RedGain = value);
            _client.SetObjectParams(_objectIndex, new { red_gain = value });
        }
    }

    public float GreenGain
    {
        get => _state.GreenGain;
        set
        {
            if (value.Equals(_state.GreenGain)) return;
            _service.UpdateObjectState(_sceneIndex, _objectIndex, s => s.GreenGain = value);
            _client.SetObjectParams(_objectIndex, new { green_gain = value });
        }
    }

    public float BlueGain
    {
        get => _state.BlueGain;
        set
        {
            if (value.Equals(_state.BlueGain)) return;
            _service.UpdateObjectState(_sceneIndex, _objectIndex, s => s.BlueGain = value);
            _client.SetObjectParams(_objectIndex, new { blue_gain = value });
        }
    }

    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand MoveLeftCommand { get; }
    public ICommand MoveRightCommand { get; }
    public ICommand UnifyCommand { get; }

    private void Nudge(int dx, int dy)
    {
        if (dx != 0) X += dx;
        if (dy != 0) Y += dy;
    }

    private void Unify()
    {
        RedGain = 1.0f;
        GreenGain = 1.0f;
        BlueGain = 1.0f;
    }

    private void OnObjectStateChanged(int sceneIndex, int objectIndex)
    {
        if (sceneIndex != _sceneIndex || objectIndex != _objectIndex)
            return;

        // May fire from the gamepad dispatcher's own background thread.
        Dispatcher.UIThread.Post(() =>
        {
            this.RaisePropertyChanged(nameof(X));
            this.RaisePropertyChanged(nameof(Y));
            this.RaisePropertyChanged(nameof(Visible));
            this.RaisePropertyChanged(nameof(FlipHorizontal));
            this.RaisePropertyChanged(nameof(FlipVertical));
            this.RaisePropertyChanged(nameof(RedGain));
            this.RaisePropertyChanged(nameof(GreenGain));
            this.RaisePropertyChanged(nameof(BlueGain));
        });
    }

    public void Dispose() => _service.ObjectStateChanged -= OnObjectStateChanged;
}
