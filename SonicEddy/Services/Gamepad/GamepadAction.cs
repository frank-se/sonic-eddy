using System;
using System.Collections.Generic;

namespace SonicEddy.Services.Gamepad;

public enum GamepadActionKind
{
    Button,
    Axis,
}

public enum GamepadAction
{
    MoveX,
    MoveY,
    NextObject,
    PreviousObject,
    ToggleHide,
    ToggleFlipVertical,
    ToggleFlipHorizontal,
    ColorSliderAxis,
    NextColorSlider,
    PreviousColorSlider,
    UnifyColor,

    // Bindable now, no effect yet - see project memory for why.
    UnifySaturationEtc,
    NextScene,
    PreviousScene,

    // T-bar M/E switcher: absolute axis position, not a relative nudge like
    // MoveX/MoveY/ColorSliderAxis - meant for a physical throttle/fader
    // whose raw position should map 1:1 onto the T-bar, no deadzone (a
    // centered deadzone would create a dead spot at the T-bar's most
    // meaningful position, the 50/50 blend point).
    TBarAxis,

    // T-bar M/E switcher: rotates whichever panel the gamepad currently
    // targets (see GamepadService.SetPreviewSide) through
    // None -> Mic1 -> Mic2 -> None - see MixEffectsSwitcherViewModel.CycleMic.
    CycleMic,
}

public static class GamepadActions
{
    public static readonly IReadOnlyList<GamepadAction> All =
        Enum.GetValues<GamepadAction>();

    public static GamepadActionKind KindOf(GamepadAction action) => action switch
    {
        GamepadAction.MoveX => GamepadActionKind.Axis,
        GamepadAction.MoveY => GamepadActionKind.Axis,
        GamepadAction.ColorSliderAxis => GamepadActionKind.Axis,
        GamepadAction.TBarAxis => GamepadActionKind.Axis,
        _ => GamepadActionKind.Button,
    };
}
