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
// T-bar M/E switcher: this service now drives one of two independent
// compositor panels (A/B, see CompositorInstanceNames) rather than a single
// fixed one. Everything that used to be a flat field on the service is
// duplicated per side in TargetState; _targetsB (flipped via
// SetPreviewSide, called by MixEffectsSwitcherViewModel as the T-bar
// crosses its midpoint) selects which one ApplyAction/ApplyContinuousAxisActions
// actually mutate. Both sides stay attached/warm continuously - flipping is
// just a pointer swap, not a reconnect, so no scene-load state is lost or
// raced when the operator moves the T-bar.
public sealed class GamepadService : IGamepadService, IDisposable
{
    private const float Deadzone = 0.15f;
    private const float CaptureThreshold = 0.5f;
    private const int NudgePerTickAtFullDeflection = 8;
    private const float GainPerTickAtFullDeflection = 0.03f;
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(50);

    // Everything that used to be a flat field on GamepadService, one
    // instance per compositor panel (A/B). Deliberately plain mutable
    // fields, no extra locking - same benign-cross-thread-race tolerance
    // the single-target version already had between the poll thread and
    // ConnectionChanged/ParamsChanged callbacks.
    private sealed class TargetState(IStreamingControlService service)
    {
        public readonly IStreamingControlService Service = service;
        public CompositorClient? Client;
        public int ActiveSceneIndex = -1;
        public SceneFileConfig? ActiveSceneFile;
        public int SelectedObjectPosition;
        public int SelectedColorChannel;
    }

    private readonly IAppDataService _appDataService;
    private readonly ConcurrentDictionary<GamepadAction, GamepadBinding> _bindings = new();
    private readonly Dictionary<byte, short> _currentAxisValues = new();
    // Separate from _currentAxisValues: GameController axis indices (0-5,
    // SDL_CONTROLLER_AXIS_*) and raw SDL joystick axis indices both start
    // at 0 and would otherwise collide in one dictionary. Only ever
    // populated for devices SDL doesn't recognize as a "game controller"
    // (see TryOpenDevice) - ordinary gamepads never touch this dictionary.
    private readonly Dictionary<byte, short> _currentJoystickAxisValues = new();

    private readonly TargetState _stateA;
    private readonly TargetState _stateB;
    private volatile bool _targetsB; // false => gamepad drives A, true => drives B

    private System.Threading.Thread? _pollThread;
    private CancellationTokenSource? _shutdownCts;
    private TaskCompletionSource<GamepadBinding>? _captureTcs;

    private TargetState Current => _targetsB ? _stateB : _stateA;

    public GamepadService(IAppDataService appDataService,
        IStreamingControlService streamingControlServiceA,
        IStreamingControlService streamingControlServiceB)
    {
        _appDataService = appDataService;
        _stateA = new TargetState(streamingControlServiceA);
        _stateB = new TargetState(streamingControlServiceB);
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

        _stateA.Service.ConnectionChanged += OnStreamingConnectionChangedA;
        _stateB.Service.ConnectionChanged += OnStreamingConnectionChangedB;
        AttachClient(_stateA, _stateA.Service.Client);
        AttachClient(_stateB, _stateB.Service.Client);

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

    private void OnStreamingConnectionChangedA() => AttachClient(_stateA, _stateA.Service.Client);
    private void OnStreamingConnectionChangedB() => AttachClient(_stateB, _stateB.Service.Client);

    private void AttachClient(TargetState state, CompositorClient? client)
    {
        if (state.Client is not null)
            state.Client.ParamsChanged -= state == _stateA ? OnParamsChangedA : OnParamsChangedB;

        state.Client = client;
        state.ActiveSceneFile = null;
        state.ActiveSceneIndex = -1;
        state.SelectedObjectPosition = 0;

        if (client is null)
            return;

        client.ParamsChanged += state == _stateA ? OnParamsChangedA : OnParamsChangedB;
        _ = LoadInitialAsync(state, client);
    }

    private async Task LoadInitialAsync(TargetState state, CompositorClient client)
    {
        var parameters = await client.GetParamsAsync();
        if (parameters is not null)
            await ApplyParamsAsync(state, parameters);
    }

    private void OnParamsChangedA(CompositorParams parameters) => _ = ApplyParamsAsync(_stateA, parameters);
    private void OnParamsChangedB(CompositorParams parameters) => _ = ApplyParamsAsync(_stateB, parameters);

    private async Task ApplyParamsAsync(TargetState state, CompositorParams parameters)
    {
        state.ActiveSceneIndex = parameters.ActiveSceneIndex;
        if (state.ActiveSceneIndex < 0 || state.ActiveSceneIndex >= parameters.Scenes.Count)
        {
            state.ActiveSceneFile = null;
            return;
        }

        state.ActiveSceneFile = await state.Service.LoadSceneFileAsync(
            parameters.Scenes[state.ActiveSceneIndex].File);
        state.SelectedObjectPosition = 0;
        BroadcastSelection(state);
    }

    // Announces the gamepad's own notion of "selected object" through the
    // shared IStreamingControlService.SelectObject channel, so the Streaming
    // Controls window and the Soomfon deck both see gamepad-driven selection
    // changes too - not just the other way around.
    private static void BroadcastSelection(TargetState state)
    {
        var objects = CombinedObjects(state);
        if (state.SelectedObjectPosition < objects.Count)
            state.Service.SelectObject(state.ActiveSceneIndex, objects[state.SelectedObjectPosition].FlatIndex);
    }

    // Combined camera-then-image ordering, matching the Streaming Controls
    // window's object picker rows but without their fixed 2/10 display
    // caps - this just walks whatever the scene file actually contains.
    private static IReadOnlyList<(int FlatIndex, SceneFileObject Object)> CombinedObjects(TargetState state)
    {
        if (state.ActiveSceneFile is null)
            return [];

        var cameras = new List<(int, SceneFileObject)>();
        var images = new List<(int, SceneFileObject)>();
        for (var i = 0; i < state.ActiveSceneFile.Objects.Count; ++i)
        {
            var obj = state.ActiveSceneFile.Objects[i];
            if (obj.IsCamera) cameras.Add((i, obj));
            else if (obj.IsImage) images.Add((i, obj));
        }
        cameras.AddRange(images);
        return cameras;
    }

    private void CycleObject(int direction)
    {
        var state = Current;
        var objects = CombinedObjects(state);
        if (objects.Count == 0) return;

        state.SelectedObjectPosition = ((state.SelectedObjectPosition + direction) % objects.Count + objects.Count) % objects.Count;
        BroadcastSelection(state);
    }

    private void MutateSelected(Action<ObjectState> mutate, Func<ObjectState, object> fieldsForWire)
    {
        var state = Current;
        if (state.Client is null || state.ActiveSceneFile is null) return;
        var objects = CombinedObjects(state);
        if (state.SelectedObjectPosition >= objects.Count) return;

        var (flatIndex, baseline) = objects[state.SelectedObjectPosition];
        var objectState = state.Service.GetOrCreateObjectState(state.ActiveSceneIndex, flatIndex, baseline);
        state.Service.UpdateObjectState(state.ActiveSceneIndex, flatIndex, mutate);
        state.Client.SetObjectParams(flatIndex, fieldsForWire(objectState));
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
                Current.SelectedColorChannel = (Current.SelectedColorChannel + 1) % 3;
                break;
            case GamepadAction.PreviousColorSlider:
                Current.SelectedColorChannel = (Current.SelectedColorChannel + 2) % 3;
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
        var target = Current;
        if (delta is null || target.Client is null || target.ActiveSceneFile is null) return;

        var objects = CombinedObjects(target);
        if (target.SelectedObjectPosition >= objects.Count) return;
        var (flatIndex, baseline) = objects[target.SelectedObjectPosition];
        var state = target.Service.GetOrCreateObjectState(target.ActiveSceneIndex, flatIndex, baseline);

        var step = (int)Math.Round(delta.Value * NudgePerTickAtFullDeflection);
        if (step == 0) return;

        if (isX)
        {
            var maxX = Math.Max(0, target.ActiveSceneFile.CanvasWidth - baseline.Width);
            var clamped = Math.Clamp(state.X + step, 0, maxX);
            if (clamped == state.X) return;
            target.Service.UpdateObjectState(target.ActiveSceneIndex, flatIndex, s => s.X = clamped);
            target.Client.SetObjectParams(flatIndex, new { dst_x = clamped });
        }
        else
        {
            var maxY = Math.Max(0, target.ActiveSceneFile.CanvasHeight - baseline.Height);
            var clamped = Math.Clamp(state.Y + step, 0, maxY);
            if (clamped == state.Y) return;
            target.Service.UpdateObjectState(target.ActiveSceneIndex, flatIndex, s => s.Y = clamped);
            target.Client.SetObjectParams(flatIndex, new { dst_y = clamped });
        }
    }

    private void ApplyColorAxis()
    {
        var delta = ReadAxisDelta(GamepadAction.ColorSliderAxis);
        var target = Current;
        if (delta is null || target.Client is null || target.ActiveSceneFile is null) return;

        var objects = CombinedObjects(target);
        if (target.SelectedObjectPosition >= objects.Count) return;
        var (flatIndex, baseline) = objects[target.SelectedObjectPosition];
        var state = target.Service.GetOrCreateObjectState(target.ActiveSceneIndex, flatIndex, baseline);

        var step = delta.Value * GainPerTickAtFullDeflection;

        switch (target.SelectedColorChannel)
        {
            case 0:
                var red = Math.Clamp(state.RedGain + step, 0f, 2f);
                target.Service.UpdateObjectState(target.ActiveSceneIndex, flatIndex, s => s.RedGain = red);
                target.Client.SetObjectParams(flatIndex, new { red_gain = red });
                break;
            case 1:
                var green = Math.Clamp(state.GreenGain + step, 0f, 2f);
                target.Service.UpdateObjectState(target.ActiveSceneIndex, flatIndex, s => s.GreenGain = green);
                target.Client.SetObjectParams(flatIndex, new { green_gain = green });
                break;
            default:
                var blue = Math.Clamp(state.BlueGain + step, 0f, 2f);
                target.Service.UpdateObjectState(target.ActiveSceneIndex, flatIndex, s => s.BlueGain = blue);
                target.Client.SetObjectParams(flatIndex, new { blue_gain = blue });
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
        _stateA.Service.ConnectionChanged -= OnStreamingConnectionChangedA;
        _stateB.Service.ConnectionChanged -= OnStreamingConnectionChangedB;
        if (_stateA.Client is not null)
            _stateA.Client.ParamsChanged -= OnParamsChangedA;
        if (_stateB.Client is not null)
            _stateB.Client.ParamsChanged -= OnParamsChangedB;

        _shutdownCts?.Cancel();
        _pollThread?.Join(TimeSpan.FromSeconds(1));
        _shutdownCts?.Dispose();
    }
}
