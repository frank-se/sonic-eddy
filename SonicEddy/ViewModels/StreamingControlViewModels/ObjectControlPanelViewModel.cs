using System;
using System.Windows.Input;
using Fr.Sonic.Compositor;
using ReactiveUI;
using SonicEddy.Services.StreamingControl;

namespace SonicEddy.ViewModels.StreamingControlViewModels;

// Local, in-memory "current value" state for one object - the compositor
// never echoes object_params back (see project_scene_file_format memory),
// so this view model is the sole source of truth for what's currently
// shown, seeded from the scene file's baseline. Every setter sends only
// the field that changed, matching the compositor's partial-update design.
public sealed class ObjectControlPanelViewModel : ViewModelBase
{
    private const int NudgeStep = 10;

    private readonly CompositorClient _client;
    private readonly int _objectIndex;
    private readonly int _objectWidth;
    private readonly int _objectHeight;

    private int _x;
    private int _y;
    private bool _visible = true;
    private bool _flipHorizontal;
    private bool _flipVertical;
    private float _redGain = 1.0f;
    private float _greenGain = 1.0f;
    private float _blueGain = 1.0f;

    public ObjectControlPanelViewModel(CompositorClient client, int objectIndex,
        SceneFileObject baseline, int canvasWidth, int canvasHeight)
    {
        _client = client;
        _objectIndex = objectIndex;
        _objectWidth = baseline.Width;
        _objectHeight = baseline.Height;
        MaxX = Math.Max(0, canvasWidth - baseline.Width);
        MaxY = Math.Max(0, canvasHeight - baseline.Height);

        _x = Math.Clamp(baseline.X, 0, MaxX);
        _y = Math.Clamp(baseline.Y, 0, MaxY);
        _flipHorizontal = baseline.FlipHorizontal;
        _flipVertical = baseline.FlipVertical;

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
        get => _x;
        set
        {
            var clamped = Math.Clamp(value, 0, MaxX);
            this.RaiseAndSetIfChanged(ref _x, clamped);
            SendUpdate(new { dst_x = clamped });
        }
    }

    public int Y
    {
        get => _y;
        set
        {
            var clamped = Math.Clamp(value, 0, MaxY);
            this.RaiseAndSetIfChanged(ref _y, clamped);
            SendUpdate(new { dst_y = clamped });
        }
    }

    public bool Visible
    {
        get => _visible;
        set
        {
            this.RaiseAndSetIfChanged(ref _visible, value);
            SendUpdate(new { visible = value });
        }
    }

    public bool FlipHorizontal
    {
        get => _flipHorizontal;
        set
        {
            this.RaiseAndSetIfChanged(ref _flipHorizontal, value);
            SendUpdate(new { flip_horizontal = value });
        }
    }

    public bool FlipVertical
    {
        get => _flipVertical;
        set
        {
            this.RaiseAndSetIfChanged(ref _flipVertical, value);
            SendUpdate(new { flip_vertical = value });
        }
    }

    public float RedGain
    {
        get => _redGain;
        set
        {
            this.RaiseAndSetIfChanged(ref _redGain, value);
            SendUpdate(new { red_gain = value });
        }
    }

    public float GreenGain
    {
        get => _greenGain;
        set
        {
            this.RaiseAndSetIfChanged(ref _greenGain, value);
            SendUpdate(new { green_gain = value });
        }
    }

    public float BlueGain
    {
        get => _blueGain;
        set
        {
            this.RaiseAndSetIfChanged(ref _blueGain, value);
            SendUpdate(new { blue_gain = value });
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

    private void SendUpdate(object fields) => _client.SetObjectParams(_objectIndex, fields);
}
