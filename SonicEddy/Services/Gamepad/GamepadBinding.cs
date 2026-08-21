namespace SonicEddy.Services.Gamepad;

// SdlValue is the raw SDL_GameControllerButton or SDL_GameControllerAxis
// enum value (Silk.NET.SDL), depending on Kind - which must match
// GamepadActions.KindOf(action) for whichever action this is bound to.
public sealed record GamepadBinding(GamepadActionKind Kind, int SdlValue);
