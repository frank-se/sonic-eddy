using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fr.Sonic.Compositor;
using Silk.NET.SDL;
using SonicEddy.Contracts.Gamepad;
using SonicEddy.Services.AppData;
using SonicEddy.Services.StreamingControl;

namespace SonicEddy.Services.Gamepad;

// Always-on background service (started at app startup, independent of any
// window) that turns physical gamepad input into the same
// CompositorClient.SetActiveSceneIndex/SetObjectParams calls the Streaming
// Controls window's buttons make - a gamepad is just another control-plane
// input source. Everything SDL-related (init, event pump, the controller
// handle) lives on one dedicated background thread - SDL expects all of
// its calls to come from the thread that initialized it.
//
// Downstream-effects pivot: the gamepad used to retarget between whichever
// T-bar M/E panel (A/B) was currently "preview" - that's gone now. Panel
// scene objects are UI-only (you can't watch a preview-side object live
// anyway, since there's no separate preview monitor in this system), and
// the gamepad is permanently, always dedicated to the single downstream
// node's 5 fixed objects instead - the one thing that's always part of the
// actual visible/recorded output, so live editing (e.g. nudging a pointer
// graphic while it's on screen) is actually useful. SetPreviewSide/_targetsB
// survives only to parameterize CycleMic (which mic-tie panel to rotate) -
// its sole remaining job.
public sealed class GamepadService : IGamepadService, IDisposable
{
    private const float Deadzone = 0.15f;
    private const float CaptureThreshold = 0.5f;
    private const int NudgePerTickAtFullDeflection = 8;
    private const float GainPerTickAtFullDeflection = 0.03f;
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(50);

    private readonly IAppDataService _appDataService;
    private readonly IStreamingControlService _downstreamService;
    private readonly ConcurrentDictionary<GamepadAction, GamepadBinding> _bindings = new();
    private readonly Dictionary<byte, short> _currentAxisValues = new();
    // Separate from _currentAxisValues: GameController axis indices (0-5,
    // SDL_CONTROLLER_AXIS_*) and raw SDL joystick axis indices both start
    // at 0 and would otherwise collide in one dictionary. Only ever
    // populated for devices SDL doesn't recognize as a "game controller"
    // (see TryOpenDevice) - ordinary gamepads never touch this dictionary.
    private readonly Dictionary<byte, short> _currentJoystickAxisValues = new();

    private CompositorClient? _client;
    private int _activeSceneIndex = -1;
    private SceneFileConfig? _activeSceneFile;
    private int _selectedObjectPosition;
    private int _selectedColorChannel;

    // Only feeds CycleMic now - see class comment.
    private volatile bool _targetsB;

    private System.Threading.Thread? _pollThread;
    private CancellationTokenSource? _shutdownCts;
    private TaskCompletionSource<GamepadBinding>? _captureTcs;

    public GamepadService(IAppDataService appDataService, IStreamingControlService downstreamService)
    {
        _appDataService = appDataService;
        _downstreamService = downstreamService;
    }

    public void SetPreviewSide(bool previewIsB) => _targetsB = previewIsB;

    public event Action<bool>? CycleMicRequested;

    public bool IsControllerConnected { get; private set; }
    public event Action? ControllerConnectionChanged;
    public IReadOnlyDictionary<GamepadAction, GamepadBinding> Bindings => _bindings;

    public async Task InitializeAsync()
    {
        var saved = await _appDataService.LoadGamepadBindingsConfig();
        if (saved is not null)
        {
            foreach (var entry in saved.Bindings)
            {
                if (Enum.TryParse<GamepadAction>(entry.ActionName, out var action))
                    _bindings[action] = new GamepadBinding(
                        entry.IsAxis ? GamepadActionKind.Axis : GamepadActionKind.Button,
                        entry.SdlValue, entry.IsJoystick);
            }
        }

        _downstreamService.ConnectionChanged += OnStreamingConnectionChanged;
        AttachClient(_downstreamService.Client);

        _shutdownCts = new CancellationTokenSource();
        _pollThread = new System.Threading.Thread(() => RunPollLoop(_shutdownCts.Token))
        {
            IsBackground = true,
            Name = "GamepadPoll",
        };
        _pollThread.Start();
    }

    public Task<GamepadBinding> CaptureNextInputAsync(CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<GamepadBinding>(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.Exchange(ref _captureTcs, tcs);
        // Timeout/cancellation (e.g. GamepadActionRowViewModel's 10s capture
        // window) must also clear _captureTcs - otherwise it's left pointing
        // at a dead, already-completed task forever, and
        // ApplyContinuousAxisActions's "skip while capturing" guard silently
        // disables all axis dispatch (Move + Color) for the rest of the
        // process's life. Only clear if it's still *this* capture, so a
        // newer capture already in flight isn't wiped out by a stale
        // cancellation registration racing behind it.
        cancellationToken.Register(() =>
        {
            tcs.TrySetCanceled(cancellationToken);
            Interlocked.CompareExchange(ref _captureTcs, null, tcs);
        });
        return tcs.Task;
    }

    public async Task SetBindingAsync(GamepadAction action, GamepadBinding binding)
    {
        _bindings[action] = binding;

        var config = new GamepadBindingsConfig
        {
            Bindings = _bindings.Select(kv => new GamepadActionBindingConfig
            {
                ActionName = kv.Key.ToString(),
                IsAxis = kv.Value.Kind == GamepadActionKind.Axis,
                SdlValue = kv.Value.SdlValue,
                IsJoystick = kv.Value.IsJoystick,
            }).ToList(),
        };
        await _appDataService.StoreGamepadBindingsConfig(config);
    }

    public string DescribeBinding(GamepadBinding? binding)
    {
        if (binding is null)
            return "Unbound";

        // Raw joystick index has no SDL-curated enum name to look up (that's
        // exactly why it's on this path - see GamepadBinding) - just show
        // the numeric index, prefixed to distinguish it from a GameController
        // binding using the same-looking index.
        if (binding.IsJoystick)
            return binding.Kind == GamepadActionKind.Button
                ? $"Joystick Button {binding.SdlValue}"
                : $"Joystick Axis {binding.SdlValue}";

        return binding.Kind == GamepadActionKind.Button
            ? $"Button {StripPrefix(((GameControllerButton)binding.SdlValue).ToString(), "ControllerButton")}"
            : $"Axis {StripPrefix(((GameControllerAxis)binding.SdlValue).ToString(), "ControllerAxis")}";
    }

    private static string StripPrefix(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.Ordinal) ? value[prefix.Length..] : value;

    private void OnStreamingConnectionChanged() => AttachClient(_downstreamService.Client);

    private void AttachClient(CompositorClient? client)
    {
        if (_client is not null)
            _client.ParamsChanged -= OnParamsChanged;

        _client = client;
        _activeSceneFile = null;
        _activeSceneIndex = -1;
        _selectedObjectPosition = 0;

        if (client is null)
            return;

        client.ParamsChanged += OnParamsChanged;
        _ = LoadInitialAsync(client);
    }

    private async Task LoadInitialAsync(CompositorClient client)
    {
        var parameters = await client.GetParamsAsync();
        if (parameters is not null)
            await ApplyParamsAsync(parameters);
    }

    private void OnParamsChanged(CompositorParams parameters) => _ = ApplyParamsAsync(parameters);

    private async Task ApplyParamsAsync(CompositorParams parameters)
    {
        _activeSceneIndex = parameters.ActiveSceneIndex;
        if (_activeSceneIndex < 0 || _activeSceneIndex >= parameters.Scenes.Count)
        {
            _activeSceneFile = null;
            return;
        }

        _activeSceneFile = await _downstreamService.LoadSceneFileAsync(
            parameters.Scenes[_activeSceneIndex].File);
        _selectedObjectPosition = 0;
        BroadcastSelection();
    }

    // Announces the gamepad's own notion of "selected object" through the
    // shared IStreamingControlService.SelectObject channel, so the Streaming
    // Controls window and the Soomfon deck both see gamepad-driven selection
    // changes too - not just the other way around.
    private void BroadcastSelection()
    {
        var objects = CombinedObjects();
        if (_selectedObjectPosition < objects.Count)
            _downstreamService.SelectObject(_activeSceneIndex, objects[_selectedObjectPosition].FlatIndex);
    }

    // Combined camera-then-image ordering, matching the Streaming Controls
    // window's object picker rows but without their fixed 2/10 display
    // caps - this just walks whatever the scene file actually contains
    // (always the downstream node's 5 fixed objects in practice).
    private IReadOnlyList<(int FlatIndex, SceneFileObject Object)> CombinedObjects()
    {
        if (_activeSceneFile is null)
            return [];

        var cameras = new List<(int, SceneFileObject)>();
        var images = new List<(int, SceneFileObject)>();
        for (var i = 0; i < _activeSceneFile.Objects.Count; ++i)
        {
            var obj = _activeSceneFile.Objects[i];
            if (obj.IsCamera) cameras.Add((i, obj));
            else if (obj.IsImage) images.Add((i, obj));
        }
        cameras.AddRange(images);
        return cameras;
    }

    private void CycleObject(int direction)
    {
        var objects = CombinedObjects();
        if (objects.Count == 0) return;

        _selectedObjectPosition = ((_selectedObjectPosition + direction) % objects.Count + objects.Count) % objects.Count;
        BroadcastSelection();
    }

    private void MutateSelected(Action<ObjectState> mutate, Func<ObjectState, object> fieldsForWire)
    {
        if (_client is null || _activeSceneFile is null) return;
        var objects = CombinedObjects();
        if (_selectedObjectPosition >= objects.Count) return;

        var (flatIndex, baseline) = objects[_selectedObjectPosition];
        var objectState = _downstreamService.GetOrCreateObjectState(_activeSceneIndex, flatIndex, baseline);
        _downstreamService.UpdateObjectState(_activeSceneIndex, flatIndex, mutate);
        _client.SetObjectParams(flatIndex, fieldsForWire(objectState));
    }

    private void ApplyAction(GamepadAction action)
    {
        switch (action)
        {
            case GamepadAction.NextObject:
                CycleObject(1);
                break;
            case GamepadAction.PreviousObject:
                CycleObject(-1);
                break;
            case GamepadAction.ToggleHide:
                MutateSelected(s => s.Visible = !s.Visible, s => new { visible = s.Visible });
                break;
            case GamepadAction.ToggleFlipVertical:
                MutateSelected(s => s.FlipVertical = !s.FlipVertical, s => new { flip_vertical = s.FlipVertical });
                break;
            case GamepadAction.ToggleFlipHorizontal:
                MutateSelected(s => s.FlipHorizontal = !s.FlipHorizontal, s => new { flip_horizontal = s.FlipHorizontal });
                break;
            case GamepadAction.NextColorSlider:
                _selectedColorChannel = (_selectedColorChannel + 1) % 3;
                break;
            case GamepadAction.PreviousColorSlider:
                _selectedColorChannel = (_selectedColorChannel + 2) % 3;
                break;
            case GamepadAction.UnifyColor:
                MutateSelected(s =>
                {
                    s.RedGain = 1.0f;
                    s.GreenGain = 1.0f;
                    s.BlueGain = 1.0f;
                }, s => new { red_gain = 1.0f, green_gain = 1.0f, blue_gain = 1.0f });
                break;
            case GamepadAction.CycleMic:
                CycleMicRequested?.Invoke(_targetsB);
                break;
            case GamepadAction.UnifySaturationEtc:
            case GamepadAction.NextScene:
            case GamepadAction.PreviousScene:
                break; // bindable, no effect yet
        }
    }

    private void ApplyContinuousAxisActions()
    {
        // Skip while capturing a new binding - otherwise rebinding an axis
        // that's already bound to something else would keep driving that
        // old action while the user is trying to press/move the new one.
        if (Volatile.Read(ref _captureTcs) is not null)
            return;

        ApplyMoveAxis(GamepadAction.MoveX, isX: true);
        ApplyMoveAxis(GamepadAction.MoveY, isX: false);
        ApplyColorAxis();
        ApplyTBarAxis();
    }

    public event Action<double>? TBarAxisChanged;

    private void ApplyTBarAxis()
    {
        var raw = ReadAxisRaw(GamepadAction.TBarAxis);
        if (raw is null) return;

        TBarAxisChanged?.Invoke(raw.Value);
    }

    // Absolute position, no deadzone - unlike ReadAxisDelta (used by the
    // relative nudge actions), a throttle/fader has no spring-loaded
    // center to filter jitter around, and a deadzone there would create a
    // dead spot at the T-bar's most meaningful position (the 50/50 point).
    private float? ReadAxisRaw(GamepadAction action)
    {
        if (!_bindings.TryGetValue(action, out var binding) || binding.Kind != GamepadActionKind.Axis)
            return null;
        var values = binding.IsJoystick ? _currentJoystickAxisValues : _currentAxisValues;
        if (!values.TryGetValue((byte)binding.SdlValue, out var raw))
            return null;

        return Math.Clamp(raw / 32768f, -1f, 1f);
    }

    private void ApplyMoveAxis(GamepadAction action, bool isX)
    {
        var delta = ReadAxisDelta(action);
        if (delta is null || _client is null || _activeSceneFile is null) return;

        var objects = CombinedObjects();
        if (_selectedObjectPosition >= objects.Count) return;
        var (flatIndex, baseline) = objects[_selectedObjectPosition];
        var state = _downstreamService.GetOrCreateObjectState(_activeSceneIndex, flatIndex, baseline);

        var step = (int)Math.Round(delta.Value * NudgePerTickAtFullDeflection);
        if (step == 0) return;

        if (isX)
        {
            var maxX = Math.Max(0, _activeSceneFile.CanvasWidth - baseline.Width);
            var clamped = Math.Clamp(state.X + step, 0, maxX);
            if (clamped == state.X) return;
            _downstreamService.UpdateObjectState(_activeSceneIndex, flatIndex, s => s.X = clamped);
            _client.SetObjectParams(flatIndex, new { dst_x = clamped });
        }
        else
        {
            var maxY = Math.Max(0, _activeSceneFile.CanvasHeight - baseline.Height);
            var clamped = Math.Clamp(state.Y + step, 0, maxY);
            if (clamped == state.Y) return;
            _downstreamService.UpdateObjectState(_activeSceneIndex, flatIndex, s => s.Y = clamped);
            _client.SetObjectParams(flatIndex, new { dst_y = clamped });
        }
    }

    private void ApplyColorAxis()
    {
        var delta = ReadAxisDelta(GamepadAction.ColorSliderAxis);
        if (delta is null || _client is null || _activeSceneFile is null) return;

        var objects = CombinedObjects();
        if (_selectedObjectPosition >= objects.Count) return;
        var (flatIndex, baseline) = objects[_selectedObjectPosition];
        var state = _downstreamService.GetOrCreateObjectState(_activeSceneIndex, flatIndex, baseline);

        var step = delta.Value * GainPerTickAtFullDeflection;

        switch (_selectedColorChannel)
        {
            case 0:
                var red = Math.Clamp(state.RedGain + step, 0f, 2f);
                _downstreamService.UpdateObjectState(_activeSceneIndex, flatIndex, s => s.RedGain = red);
                _client.SetObjectParams(flatIndex, new { red_gain = red });
                break;
            case 1:
                var green = Math.Clamp(state.GreenGain + step, 0f, 2f);
                _downstreamService.UpdateObjectState(_activeSceneIndex, flatIndex, s => s.GreenGain = green);
                _client.SetObjectParams(flatIndex, new { green_gain = green });
                break;
            default:
                var blue = Math.Clamp(state.BlueGain + step, 0f, 2f);
                _downstreamService.UpdateObjectState(_activeSceneIndex, flatIndex, s => s.BlueGain = blue);
                _client.SetObjectParams(flatIndex, new { blue_gain = blue });
                break;
        }
    }

    // Returns the normalized (-1..1) deflection for the given axis action's
    // binding, or null if unbound/within the deadzone.
    private float? ReadAxisDelta(GamepadAction action)
    {
        if (!_bindings.TryGetValue(action, out var binding) || binding.Kind != GamepadActionKind.Axis)
            return null;
        var values = binding.IsJoystick ? _currentJoystickAxisValues : _currentAxisValues;
        if (!values.TryGetValue((byte)binding.SdlValue, out var raw))
            return null;

        var normalized = raw / 32768f;
        return Math.Abs(normalized) < Deadzone ? null : normalized;
    }

    private unsafe void RunPollLoop(CancellationToken token)
    {
        using var sdl = Sdl.GetApi();
        sdl.Init(Sdl.InitGamecontroller | Sdl.InitJoystick);

        GameController* controller = null;
        // Instance id -> Joystick* (as IntPtr, since a field/local dictionary
        // can't hold an unsafe pointer type directly). Only ever holds
        // devices SDL doesn't recognize as a "game controller" - see
        // TryOpenDevice. Unlike `controller`, this supports any number of
        // such devices simultaneously (no reason to cap it at one).
        var joysticks = new Dictionary<int, IntPtr>();
        try
        {
            for (var i = 0; i < sdl.NumJoysticks(); ++i)
                TryOpenDevice(sdl, i, ref controller, joysticks);

            var lastTick = DateTime.UtcNow;
            var ev = default(Event);
            while (!token.IsCancellationRequested)
            {
                while (sdl.PollEvent(ref ev) != 0)
                    HandleEvent(sdl, ref ev, ref controller, joysticks);

                var now = DateTime.UtcNow;
                if (now - lastTick >= TickInterval)
                {
                    ApplyContinuousAxisActions();
                    lastTick = now;
                }

                System.Threading.Thread.Sleep(5);
            }
        }
        finally
        {
            if (controller != null)
                sdl.GameControllerClose(controller);
            foreach (var ptr in joysticks.Values)
                sdl.JoystickClose((Joystick*)ptr);
            sdl.Quit();
        }
    }

    // Opens one newly-seen device by its SDL device index (NOT a stable
    // instance id - that distinction matters for *DeviceAdded/Removed
    // events, see HandleEvent). A device recognized as a game controller
    // becomes `controller` (unchanged single-controller behavior - `if
    // (controller != null) return` also makes this safe to call twice for
    // the same device, since SDL fires both *DeviceAdded event kinds for
    // controller-capable hardware and event ordering isn't guaranteed).
    // Anything else (e.g. a flight-sim throttle/HOTAS with no entry in
    // SDL's curated mapping DB) is opened via the lower-level Joystick API
    // instead, so its raw axes/buttons are still usable - see
    // GamepadBinding.IsJoystick.
    private unsafe void TryOpenDevice(Sdl sdl, int deviceIndex, ref GameController* controller,
        Dictionary<int, IntPtr> joysticks)
    {
        if (sdl.IsGameController(deviceIndex) == SdlBool.True)
        {
            if (controller != null) return;
            controller = sdl.GameControllerOpen(deviceIndex);
            if (controller == null) return;

            UpdateConnectedState(controller, joysticks);
            return;
        }

        var joystick = sdl.JoystickOpen(deviceIndex);
        if (joystick == null) return;

        var instanceId = sdl.JoystickInstanceID(joystick);
        joysticks[instanceId] = (IntPtr)joystick;
        UpdateConnectedState(controller, joysticks);
    }

    private unsafe void UpdateConnectedState(GameController* controller, Dictionary<int, IntPtr> joysticks)
    {
        var connected = controller != null || joysticks.Count > 0;
        if (connected == IsControllerConnected) return;

        IsControllerConnected = connected;
        ControllerConnectionChanged?.Invoke();
    }

    private unsafe void HandleEvent(Sdl sdl, ref Event ev, ref GameController* controller,
        Dictionary<int, IntPtr> joysticks)
    {
        switch ((EventType)ev.Type)
        {
            // *DeviceAdded's `Which` is a device INDEX (position in the
            // current enumeration), not an instance id - matches
            // TryOpenDevice's parameter. Both event kinds route through the
            // same call since a controller-capable device fires both.
            case EventType.Controllerdeviceadded:
                TryOpenDevice(sdl, ev.Cdevice.Which, ref controller, joysticks);
                break;
            case EventType.Joydeviceadded:
                TryOpenDevice(sdl, ev.Jdevice.Which, ref controller, joysticks);
                break;

            // *DeviceRemoved's `Which` IS the stable instance id (unlike
            // *DeviceAdded above - a well-known SDL asymmetry).
            case EventType.Controllerdeviceremoved:
                if (controller != null)
                {
                    sdl.GameControllerClose(controller);
                    controller = null;
                    UpdateConnectedState(controller, joysticks);
                }
                break;
            case EventType.Joydeviceremoved:
                if (joysticks.Remove(ev.Jdevice.Which, out var removed))
                {
                    sdl.JoystickClose((Joystick*)removed);
                    UpdateConnectedState(controller, joysticks);
                }
                break;

            case EventType.Controllerbuttondown:
                if (ev.Cbutton.State != 1) break;
                if (TryCompleteCapture(new GamepadBinding(GamepadActionKind.Button, ev.Cbutton.Button)))
                    break;
                DispatchButton(ev.Cbutton.Button, isJoystick: false);
                break;

            case EventType.Controlleraxismotion:
                _currentAxisValues[ev.Caxis.Axis] = ev.Caxis.Value;
                var normalized = ev.Caxis.Value / 32768f;
                if (Math.Abs(normalized) >= CaptureThreshold)
                    TryCompleteCapture(new GamepadBinding(GamepadActionKind.Axis, ev.Caxis.Axis));
                break;

            // A device opened via GameControllerOpen also generates these
            // lower-level Joystick events for the same physical hardware -
            // the `joysticks` dict only ever contains devices opened via
            // the raw Joystick path (see TryOpenDevice), so this check is
            // what keeps a real game controller's input from being
            // processed (and dispatched) twice.
            case EventType.Joybuttondown:
                if (!joysticks.ContainsKey(ev.Jbutton.Which)) break;
                if (ev.Jbutton.State != 1) break;
                if (TryCompleteCapture(new GamepadBinding(GamepadActionKind.Button, ev.Jbutton.Button, IsJoystick: true)))
                    break;
                DispatchButton(ev.Jbutton.Button, isJoystick: true);
                break;

            case EventType.Joyaxismotion:
                if (!joysticks.ContainsKey(ev.Jaxis.Which)) break;
                _currentJoystickAxisValues[ev.Jaxis.Axis] = ev.Jaxis.Value;
                var jNormalized = ev.Jaxis.Value / 32768f;
                if (Math.Abs(jNormalized) >= CaptureThreshold)
                    TryCompleteCapture(new GamepadBinding(GamepadActionKind.Axis, ev.Jaxis.Axis, IsJoystick: true));
                break;
        }
    }

    private bool TryCompleteCapture(GamepadBinding binding)
    {
        var tcs = Interlocked.Exchange(ref _captureTcs, null);
        if (tcs is null)
            return false;

        tcs.TrySetResult(binding);
        return true;
    }

    private void DispatchButton(byte sdlButton, bool isJoystick)
    {
        foreach (var (action, binding) in _bindings)
        {
            if (binding.Kind == GamepadActionKind.Button && binding.SdlValue == sdlButton &&
                binding.IsJoystick == isJoystick)
                ApplyAction(action);
        }
    }

    public void Dispose()
    {
        _downstreamService.ConnectionChanged -= OnStreamingConnectionChanged;
        if (_client is not null)
            _client.ParamsChanged -= OnParamsChanged;

        _shutdownCts?.Cancel();
        _pollThread?.Join(TimeSpan.FromSeconds(1));
        _shutdownCts?.Dispose();
    }
}
