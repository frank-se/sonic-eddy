using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using ReactiveUI;
using Splat;
using SonicEddy.Services.Gamepad;

namespace SonicEddy.ViewModels.GamepadSetupViewModels;

public sealed class GamepadSetupViewModel : ViewModelBase, IDisposable
{
    private readonly IGamepadService _service;

    public GamepadSetupViewModel()
    {
        _service = Locator.Current.GetService<IGamepadService>()!;

        Rows = new ObservableCollection<GamepadActionRowViewModel>();
        foreach (var action in GamepadActions.All)
            Rows.Add(new GamepadActionRowViewModel(_service, action));

        IsControllerConnected = _service.IsControllerConnected;
        _service.ControllerConnectionChanged += OnControllerConnectionChanged;
    }

    public ObservableCollection<GamepadActionRowViewModel> Rows { get; }

    public bool IsControllerConnected
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    private void OnControllerConnectionChanged() =>
        Dispatcher.UIThread.Post(() => IsControllerConnected = _service.IsControllerConnected);

    public void Dispose() => _service.ControllerConnectionChanged -= OnControllerConnectionChanged;
}
