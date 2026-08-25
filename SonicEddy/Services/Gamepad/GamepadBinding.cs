namespace SonicEddy.Services.Gamepad;

// SdlValue is the raw SDL_GameControllerButton or SDL_GameControllerAxis
// enum value (Silk.NET.SDL), depending on Kind - which must match
// GamepadActions.KindOf(action) for whichever action this is bound to.
// Unless IsJoystick is set, in which case SdlValue is instead a raw SDL
// joystick axis/button index (from a device SDL doesn't recognize as a
// "game controller", e.g. a flight-sim throttle/HOTAS) - GameController and
// raw Joystick index spaces both start at 0 and can collide, so this flag
// is load-bearing for correct dispatch, not just descriptive.
public sealed record GamepadBinding(GamepadActionKind Kind, int SdlValue, bool IsJoystick = false);
