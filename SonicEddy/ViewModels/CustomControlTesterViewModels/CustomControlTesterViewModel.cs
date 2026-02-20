using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia.Controls;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;

namespace SonicEddy.ViewModels.CustomControlTesterViewModels;

public class CustomControlTesterViewModel : ViewModelBase,
    IActivatableViewModel, IRoutableViewModel, IDisposable
{
    private CompositeDisposable _disposables = new();

    public CustomControlTesterViewModel(string? urlPathSegment,
        IScreen hostScreen)
    {
        UrlPathSegment = urlPathSegment;
        HostScreen = hostScreen;

        this.WhenAnyValue(x => x.SelectedPage)
            .Subscribe(_ => Console.WriteLine("Triggered"))
            .DisposeWith(_disposables);
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

    public ObservableCollection<IRoutingTarget> RoutingTargets { get; } =
    [
        new TestRoutingTarget() { Name = "Target 1" },
        new TestRoutingTarget() { Name = "Target 2" },
        new TestRoutingTarget() { Name = "Target 3" },
        new TestRoutingTarget() { Name = "Target 4" },
        new TestRoutingTarget() { Name = "Target 5" },
        new TestRoutingTarget() { Name = "Target 6" },
    ];

    public string ComboBoxName { get; } = "Route to";

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

    public ObservableCollection<PluginPageSelectorPluginPageCount> PageCounts
    {
        get;
    } =
    [
        new("Compressor", 2),
        new("Equalizer", 1),
        new("Saturator", 3)
    ];

    public ObservableCollection<ParameterCollection> ParameterCollections
    {
        get;
    } =
    [
        new ParameterCollection("Compressor", [
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
        ]),
        new ParameterCollection("Equalizer", [
            new TestParameter()
            {
                IsMainParameter = true,
                Name = "High",
                Value = 0.5f
            },
            new TestParameter()
            {
                IsMainParameter = true,
                Name = "Mid",
                Value = 0.5f
            },
            new TestParameter()
            {
                IsMainParameter = true,
                Name = "Low",
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
            new TestParameter()
            {
                IsMainParameter = true,
                Name = "Threshold",
                Value = 0.5f
            },
        ]),
    ];

    public PluginPageSelectorSelectedPage? SelectedPage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string ParameterGridText { get; } = "Compressor";

    public string? UrlPathSegment { get; }
    public IScreen HostScreen { get; }
    public ViewModelActivator Activator { get; } = new();

    public void Dispose()
    {
        _disposables.Dispose();
        Activator.Dispose();
        GC.SuppressFinalize(this);
    }
}