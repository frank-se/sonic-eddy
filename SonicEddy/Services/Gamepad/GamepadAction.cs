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
        _ => GamepadActionKind.Button,
    };
}
