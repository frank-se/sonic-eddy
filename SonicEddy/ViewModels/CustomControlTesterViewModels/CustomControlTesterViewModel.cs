using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;

namespace SonicEddy.ViewModels.CustomControlTesterViewModels;

public class CustomControlTesterViewModel : ViewModelBase,
    IActivatableViewModel, IRoutableViewModel
{
    public CustomControlTesterViewModel(string? urlPathSegment,
        IScreen hostScreen)
    {
        UrlPathSegment = urlPathSegment;
        HostScreen = hostScreen;
    }

    public ObservableCollection<IParameter> Parameters { get; } =
    [
        new TestParameter()
        {
            IsMainParameter = true,
            Name = "Threshold",
            Value = 0.5f
        },
        new TestParameter()
        {
            IsMainParameter = true,
            Name = "Threshold",
            Value = 0.5f
        },
        new TestParameter()
        {
            IsMainParameter = true,
            Name = "Threshold",
            Value = 0.5f
        },
        new TestParameter()
        {
            IsMainParameter = true,
            Name = "Threshold",
            Value = 0.5f
        },
        new TestParameter()
        {
            IsMainParameter = true,
            Name = "Threshold",
            Value = 0.5f
        },
    ];

    public bool IsSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public void SwitchSelectedChannelState()
    {
        IsSelected = !IsSelected;
    }

    public void DeleteButtonAction(string param)
    {
        Console.WriteLine($"Delete {param}");
    }

    public bool HasFilter
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public void FilterSectionDeleteAction(string param)
    {
        Console.WriteLine($"Command parameter {param}");
        HasFilter = !HasFilter;
    }

    public void FilterSectionAddAction(string param)
    {
        Console.WriteLine($"Command parameter {param}");
        HasFilter = !HasFilter;
    }

    public string? UrlPathSegment { get; }
    public IScreen HostScreen { get; }
    public ViewModelActivator Activator { get; } = new();
}