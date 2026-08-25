using ProtoBuf;

namespace SonicEddy.Contracts.Gamepad;

[ProtoContract]
public sealed class GamepadBindingsConfig
{
    [ProtoMember(1)]
    public List<GamepadActionBindingConfig> Bindings { get; set; } = [];
}

// ActionName matches SonicEddy.Services.Gamepad.GamepadAction's enum name -
// stored as a string (not the enum type itself) so this Contracts-layer
// record doesn't need a reference to the app-layer project, mirroring
// CameraRouterConfig's plain-primitives convention.
[ProtoContract]
public sealed class GamepadActionBindingConfig
{
    [ProtoMember(1)]
    public string ActionName { get; set; } = string.Empty;

    [ProtoMember(2)]
    public bool IsAxis { get; set; }

    [ProtoMember(3)]
    public int SdlValue { get; set; }

    // True when SdlValue is a raw SDL joystick axis/button index (a device
    // SDL doesn't recognize as a "game controller", e.g. a flight-sim
    // throttle/HOTAS) rather than a GameControllerAxis/GameControllerButton
    // enum value. New field, defaults false - existing saved configs (all
    // GameController-sourced) round-trip unchanged.
    [ProtoMember(4)]
    public bool IsJoystick { get; set; }
}
