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
}
