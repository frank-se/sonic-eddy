using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SonicEddy.Services.Gamepad;

public interface IGamepadService
{
    Task InitializeAsync();

    bool IsControllerConnected { get; }
    event Action? ControllerConnectionChanged;

    IReadOnlyDictionary<GamepadAction, GamepadBinding> Bindings { get; }

    // Suppresses normal action dispatch until the next button press or a
    // significant axis deflection, then resolves with that input as a
    // binding - used by the Setup Gamepad window's "Bind..." flow.
    Task<GamepadBinding> CaptureNextInputAsync(CancellationToken cancellationToken);

    Task SetBindingAsync(GamepadAction action, GamepadBinding binding);

    string DescribeBinding(GamepadBinding? binding);

    // Which of the two T-bar M/E switcher compositor panels the gamepad
    // currently drives - see MixEffectsSwitcherViewModel, which calls this
    // as the T-bar crosses its midpoint so the gamepad always targets
    // whichever side is currently "preview" (not live).
    void SetPreviewSide(bool previewIsB);
}
