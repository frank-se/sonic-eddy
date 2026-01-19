using System.Collections.ObjectModel;
using ReactiveUI;

namespace SonicEddy.ViewModels.CreateModuleDialogViewModels;

public class NodePropertiesViewModel(
    string mediaClass,
    ObservableCollection<TargetObjectViewModel> possibleTargetObjects)
    : ReactiveObject
{
    public string Name
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string Description
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;
    
    public bool AutoConnect
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool DontFallback
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool Linger
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string MediaClass { get; init; } = mediaClass;

    public TargetObjectViewModel? TargetObject
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ObservableCollection<TargetObjectViewModel> PossibleTargetObjects
    {
        get;
    } = possibleTargetObjects;

    public bool IsValid => Name != string.Empty && Description != string.Empty;
}