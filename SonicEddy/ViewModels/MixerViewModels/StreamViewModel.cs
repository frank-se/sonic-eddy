using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;

namespace SonicEddy.ViewModels.MixerViewModels;

public class StreamViewModel(
    string name,
    ulong objectSerial,
    double volume,
    string description,
    TargetObject? targetObject,
    double pan,
    bool stereo,
    ulong objectId,
    ObservableCollection<TargetObject> availableTargetObjects,
    ReactiveCommand<StreamViewModel, Unit> removeStreamCommand) : ReactiveObject
{
    public string DisplayName => $"{Name} ({ObjectId})";

    public string Name
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = name;

    public string Description
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = description;

    public ulong ObjectSerial
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = objectSerial;

    public ulong ObjectId { get; } = objectId;

    public double Volume
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = volume;

    public double Pan
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = pan;

    public bool IsStereo => stereo;

    public double Minimum => 0.0;
    public double Maximum => 1.5;

    public TargetObject? TargetObject
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = targetObject;

    public ObservableCollection<TargetObject> AvailableTargetObjects
    {
        get;
        set;
    } = availableTargetObjects;

    public ReactiveCommand<StreamViewModel, Unit> RemoveStreamCommand { get; } =
        removeStreamCommand;
}