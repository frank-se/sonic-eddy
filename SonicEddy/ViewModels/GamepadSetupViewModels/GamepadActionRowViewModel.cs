using System;
using System.Threading;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using SonicEddy.Services.Gamepad;

namespace SonicEddy.ViewModels.GamepadSetupViewModels;

public sealed class GamepadActionRowViewModel : ViewModelBase
{
    private readonly IGamepadService _service;
    private CancellationTokenSource? _captureCts;

    public GamepadActionRowViewModel(IGamepadService service, GamepadAction action)
    {
        _service = service;
        Action = action;
        DisplayName = DescribeAction(action);
        RefreshBinding();

        BindCommand = ReactiveCommand.CreateFromTask(StartCaptureAsync);
    }

    public GamepadAction Action { get; }
    public string DisplayName { get; }

    public string BindingText
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "Unbound";

    public bool IsCapturing
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ICommand BindCommand { get; }

    public void RefreshBinding()
    {
        _service.Bindings.TryGetValue(Action, out var binding);
        BindingText = _service.DescribeBinding(binding);
    }

    private async System.Threading.Tasks.Task StartCaptureAsync()
    {
        _captureCts?.Cancel();
        _captureCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        IsCapturing = true;
        BindingText = "Press a button or move an axis...";

        try
        {
            var binding = await _service.CaptureNextInputAsync(_captureCts.Token);
            await _service.SetBindingAsync(Action, binding);
        }
        catch (OperationCanceledException)
        {
            // timed out or cancelled - fall through to refresh, which
            // restores whatever was bound before (or "Unbound").
        }
        finally
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsCapturing = false;
                RefreshBinding();
            });
        }
    }

    private static string DescribeAction(GamepadAction action) => action switch
    {
        GamepadAction.MoveX => "Move X (axis)",
        GamepadAction.MoveY => "Move Y (axis)",
        GamepadAction.NextObject => "Next Object",
        GamepadAction.PreviousObject => "Previous Object",
        GamepadAction.ToggleHide => "Hide / Show",
        GamepadAction.ToggleFlipVertical => "Flip Vertical",
        GamepadAction.ToggleFlipHorizontal => "Flip Horizontal",
        GamepadAction.ColorSliderAxis => "Color Slider (axis)",
        GamepadAction.NextColorSlider => "Next Color Slider",
        GamepadAction.PreviousColorSlider => "Previous Color Slider",
        GamepadAction.UnifyColor => "Unify Color (RGB)",
        GamepadAction.UnifySaturationEtc => "Unify Saturation/etc. (not wired up yet)",
        GamepadAction.NextScene => "Next Scene (not wired up yet)",
        GamepadAction.PreviousScene => "Previous Scene (not wired up yet)",
        _ => action.ToString(),
    };
}
